import CryptoKit
import Foundation
import Security

/// Holds this device's push keypair. The private half never leaves the device and is never sent to any
/// backend, which is what makes a payload sealed against the public half unreadable by Piro's operator,
/// by heva's relay, and by APNs (RFC 0017).
///
/// The curve is NIST P-256 to match the backend: .NET's `ECDiffieHellman` cannot do X25519, so the
/// sealer standardised on P-256 across all three platforms.
///
/// Stored in the Keychain with `kSecAttrAccessibleAfterFirstUnlock` rather than
/// `WhenUnlockedThisDeviceOnly`: a Notification Service Extension has to decrypt while the phone is
/// locked, which is the entire point of a push. `ThisDeviceOnly` keeps it out of iCloud backups, so a
/// restored backup simply generates a fresh keypair and re-registers rather than resurrecting a key
/// that other devices might hold.
enum PushKeyStore {

    /// Shared with the notification service extension, so both processes see the same key. Without
    /// this the extension generates its own keypair and can never decrypt what the backend sealed for
    /// the app.
    ///
    /// This is the app's *default* access group — its application-identifier — rather than a custom
    /// one. A custom group has to be registered on the App ID and present in the provisioning profile;
    /// declaring it only in the entitlements file is not enough, and the Keychain then denies every
    /// call with errSecMissingEntitlement, which surfaces as a device that registers with no push key
    /// at all. The default group is authorised by every profile automatically, and both targets list
    /// it in their entitlements, so it shares just as well with nothing to configure.
    static let accessGroup: String? = "GNUVS35QW5.co.heva.piro"

    private static let service = "co.heva.piro.push"
    private static let account = "push-private-p256"

    /// Uncompressed EC point: `0x04 || X(32) || Y(32)` — the exact encoding the backend imports.
    private static let publicKeyLength = 65

    // MARK: - Public API

    /// The device's public key as base64url of an uncompressed EC point, generating and persisting a
    /// keypair on first call. Returns nil only if the Keychain refuses to store the key, in which case
    /// the device registers unsealed rather than failing to register at all.
    static func publicKeyBase64Url() -> String? {
        guard let key = ensureKeyPair() else { return nil }
        return base64UrlEncode(key.publicKey.x963Representation)
    }

    /// The private key, for agreeing with the ephemeral key inside a sealed envelope.
    static func privateKey() -> P256.KeyAgreement.PrivateKey? {
        ensureKeyPair()
    }

    /// True once a keypair exists, i.e. this device can receive sealed pushes.
    static var hasKey: Bool {
        loadPrivateKey() != nil
    }

    // MARK: - Keychain

    private static func ensureKeyPair() -> P256.KeyAgreement.PrivateKey? {
        if let existing = loadPrivateKey() { return existing }

        let key = P256.KeyAgreement.PrivateKey()
        return store(key) ? key : nil
    }

    private static func loadPrivateKey() -> P256.KeyAgreement.PrivateKey? {
        var query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        if let accessGroup { query[kSecAttrAccessGroup as String] = accessGroup }

        var item: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess,
              let data = item as? Data else { return nil }

        // A key that no longer parses is worse than no key: it would fail every decrypt silently. Drop
        // it so the next call generates a fresh one and the device re-registers.
        guard let key = try? P256.KeyAgreement.PrivateKey(rawRepresentation: data) else {
            delete()
            return nil
        }
        return key
    }

    @discardableResult
    private static func store(_ key: P256.KeyAgreement.PrivateKey) -> Bool {
        delete()   // SecItemAdd fails on a duplicate rather than replacing

        var attributes: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecValueData as String: key.rawRepresentation,
            // AfterFirstUnlock, not WhenUnlocked: the NSE decrypts on a locked phone.
            // ThisDeviceOnly keeps it out of backups — a restored device should re-key, not inherit.
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
        ]
        if let accessGroup { attributes[kSecAttrAccessGroup as String] = accessGroup }

        let status = SecItemAdd(attributes as CFDictionary, nil)
        if status != errSecSuccess {
            // Logged rather than swallowed: a failure here means the device registers with no push key
            // and silently receives unsealed pushes, which looks identical to success from the UI.
            // errSecMissingEntitlement (-34018) is the one to expect — it means the access group is not
            // in the provisioning profile, not merely absent from the entitlements file.
            let reason = SecCopyErrorMessageString(status, nil) as String? ?? "unknown"
            NSLog("[PushKeyStore] could not store the push key: OSStatus \(status) — \(reason)")
        }
        return status == errSecSuccess
    }

    private static func delete() {
        var query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        if let accessGroup { query[kSecAttrAccessGroup as String] = accessGroup }
        SecItemDelete(query as CFDictionary)
    }

    // MARK: - base64url

    /// base64url without padding, matching what the backend's `Base64Url` emits and expects.
    static func base64UrlEncode(_ data: Data) -> String {
        data.base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }

    static func base64UrlDecode(_ string: String) -> Data? {
        var s = string
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        // Restore the padding base64url drops; Foundation's decoder requires it.
        let remainder = s.count % 4
        if remainder > 0 { s += String(repeating: "=", count: 4 - remainder) }
        return Data(base64Encoded: s)
    }
}
