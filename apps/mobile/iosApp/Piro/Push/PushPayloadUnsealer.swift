import CryptoKit
import Foundation

/// What the backend sealed for this device.
struct PushPayload: Equatable {
    let title: String
    let body: String
    let eventKey: String
    let alertId: Int
    let url: String?
}

enum PushUnsealError: Error, Equatable {
    case notBase64Url
    case malformedEnvelope
    case unsupportedVersion(Int)
    case badNonceLength(Int)
    case ciphertextTooShort
    case noPrivateKey
    case decryptionFailed
}

/// Opens an envelope sealed by the backend's `PushPayloadSealer` (RFC 0017).
///
/// The scheme, which must stay byte-for-byte in step with the server and the Android client:
///  - ECDH P-256 between this device's private key and the ephemeral public key in the envelope
///  - HKDF-SHA256 over the shared secret, salted with that ephemeral public key, info `piro-push-v1`
///  - AES-256-GCM, 12-byte nonce, 16-byte tag appended to the ciphertext, AAD `piro-push-v1`
///
/// The ephemeral public key travels in the clear on purpose: it is not a secret, and without this
/// device's private key it yields nothing. That is what lets a relay forward a payload it cannot read.
///
/// Verified against an envelope produced by the real server sealer — the interop that matters here is
/// with `PushPayloadSealer`, not with a Swift-side round trip, since a self-consistent bug would pass
/// the latter. There is no iOS test target yet to keep that check running; this vector reproduces it:
///
///     private key (base64url raw): 6Z--VmFjwVhHk-aUJz1zi-CN8eOC2IWv4tznQuzR_3U
///     decrypts to: title "Critical alert", body "heva-api is down",
///                  eventKey "alert.raised", alertId 4242, url "piro://alert/4242"
enum PushPayloadUnsealer {

    private static let version = 1
    private static let associatedData = Data("piro-push-v1".utf8)
    private static let hkdfInfo = Data("piro-push-v1".utf8)
    private static let nonceLength = 12
    private static let tagLength = 16

    /// Decrypts an envelope using the key held in the Keychain.
    static func unseal(_ envelopeBase64Url: String) throws -> PushPayload {
        guard let key = PushKeyStore.privateKey() else { throw PushUnsealError.noPrivateKey }
        return try unseal(envelopeBase64Url, with: key)
    }

    /// Testable overload taking the key directly.
    static func unseal(
        _ envelopeBase64Url: String,
        with privateKey: P256.KeyAgreement.PrivateKey
    ) throws -> PushPayload {
        guard let envelopeData = PushKeyStore.base64UrlDecode(envelopeBase64Url) else {
            throw PushUnsealError.notBase64Url
        }
        guard let envelope = try? JSONSerialization.jsonObject(with: envelopeData) as? [String: Any] else {
            throw PushUnsealError.malformedEnvelope
        }

        // The version is checked before anything else and is also bound into the AAD, so a downgrade
        // fails to decrypt rather than being silently reinterpreted.
        guard let v = envelope["V"] as? Int else { throw PushUnsealError.malformedEnvelope }
        guard v == version else { throw PushUnsealError.unsupportedVersion(v) }

        guard let epk = envelope["Epk"] as? String,
              let n = envelope["N"] as? String,
              let ct = envelope["Ct"] as? String,
              let ephemeralRaw = PushKeyStore.base64UrlDecode(epk),
              let nonceData = PushKeyStore.base64UrlDecode(n),
              let sealedData = PushKeyStore.base64UrlDecode(ct)
        else { throw PushUnsealError.malformedEnvelope }

        guard nonceData.count == nonceLength else {
            throw PushUnsealError.badNonceLength(nonceData.count)
        }
        guard sealedData.count > tagLength else { throw PushUnsealError.ciphertextTooShort }

        guard let ephemeralPublic = try? P256.KeyAgreement.PublicKey(x963Representation: ephemeralRaw) else {
            throw PushUnsealError.malformedEnvelope
        }

        guard let shared = try? privateKey.sharedSecretFromKeyAgreement(with: ephemeralPublic) else {
            throw PushUnsealError.decryptionFailed
        }

        // Salted with the ephemeral public key so two pushes to the same device never derive the same
        // content key. CryptoKit zeroes the derived material itself when it goes out of scope.
        let key = shared.hkdfDerivedSymmetricKey(
            using: SHA256.self,
            salt: ephemeralRaw,
            sharedInfo: hkdfInfo,
            outputByteCount: 32)

        // The server appends the tag to the ciphertext, which is exactly what `combined` expects once
        // the nonce is prefixed.
        guard let nonce = try? AES.GCM.Nonce(data: nonceData),
              let box = try? AES.GCM.SealedBox(
                  combined: Data(nonce) + sealedData),
              let plaintext = try? AES.GCM.open(box, using: key, authenticating: associatedData)
        else { throw PushUnsealError.decryptionFailed }

        guard let json = try? JSONSerialization.jsonObject(with: plaintext) as? [String: Any] else {
            throw PushUnsealError.malformedEnvelope
        }

        return PushPayload(
            title: json["title"] as? String ?? "",
            body: json["body"] as? String ?? "",
            eventKey: json["eventKey"] as? String ?? "",
            alertId: json["alertId"] as? Int ?? 0,
            // A JSON null decodes to NSNull, which would render as "<null>" if passed through.
            url: (json["url"] as? String).flatMap { $0.isEmpty ? nil : $0 })
    }
}
