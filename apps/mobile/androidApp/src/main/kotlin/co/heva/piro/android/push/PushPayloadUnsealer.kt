package co.heva.piro.android.push

import android.content.Context
import android.util.Base64
import org.json.JSONObject
import java.security.PrivateKey
import javax.crypto.Cipher
import javax.crypto.KeyAgreement
import javax.crypto.Mac
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

/** What the backend sealed for this device. Mirrors the fields the cleartext data map used to carry. */
data class PushPayload(
    val title: String,
    val body: String,
    val eventKey: String,
    val alertId: Int,
    val url: String?,
)

/**
 * Opens an envelope sealed by the backend's PushPayloadSealer.
 *
 * The scheme, which must stay byte-for-byte in step with the server:
 *  - ECDH P-256 between this device's private key and the ephemeral public key carried in the envelope
 *  - HKDF-SHA256 over the shared secret, salted with that ephemeral public key, info "piro-push-v1"
 *  - AES-256-GCM, 12-byte nonce, 16-byte tag appended to the ciphertext, AAD "piro-push-v1"
 *
 * The ephemeral public key travels in the clear on purpose: it is not a secret, and without this
 * device's private key it yields nothing. That is what lets the relay forward a payload it cannot read.
 */
object PushPayloadUnsealer {

    private const val VERSION = 1
    private const val AAD = "piro-push-v1"
    private const val HKDF_INFO = "piro-push-v1"
    private const val NONCE_BYTES = 12
    private const val TAG_BITS = 128

    /**
     * Decrypts [envelopeBase64Url] and parses the payload.
     *
     * @throws IllegalArgumentException if the envelope is malformed or its version is unsupported.
     * @throws javax.crypto.AEADBadTagException if it was not sealed for this device.
     */
    fun unseal(context: Context, envelopeBase64Url: String): PushPayload =
        unseal(PushKeyStore.privateKey(context), envelopeBase64Url)

    /** Testable overload taking the key directly, so unit tests need no Android context. */
    internal fun unseal(privateKey: PrivateKey, envelopeBase64Url: String): PushPayload {
        val envelope = JSONObject(String(decodeBase64Url(envelopeBase64Url), Charsets.UTF_8))

        val version = envelope.getInt("V")
        require(version == VERSION) { "Unsupported push envelope version $version" }

        val ephemeralRaw = decodeBase64Url(envelope.getString("Epk"))
        val nonce = decodeBase64Url(envelope.getString("N"))
        val sealed = decodeBase64Url(envelope.getString("Ct"))

        require(nonce.size == NONCE_BYTES) { "Nonce must be $NONCE_BYTES bytes, got ${nonce.size}" }
        require(sealed.size > TAG_BITS / 8) { "Ciphertext is too short to carry a tag" }

        val agreement = KeyAgreement.getInstance("ECDH").apply {
            init(privateKey)
            doPhase(PushKeyStore.publicKeyFromRawPoint(ephemeralRaw), true)
        }
        val shared = agreement.generateSecret()

        val key = hkdfSha256(
            ikm = shared,
            salt = ephemeralRaw,
            info = HKDF_INFO.toByteArray(Charsets.US_ASCII),
            length = 32,
        )

        val plaintext = try {
            Cipher.getInstance("AES/GCM/NoPadding").run {
                init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(TAG_BITS, nonce))
                updateAAD(AAD.toByteArray(Charsets.US_ASCII))
                doFinal(sealed)
            }
        } finally {
            key.fill(0)
            shared.fill(0)
        }

        return parse(JSONObject(String(plaintext, Charsets.UTF_8)))
    }

    private fun parse(json: JSONObject) = PushPayload(
        title = json.optString("title", ""),
        body = json.optString("body", ""),
        eventKey = json.optString("eventKey", ""),
        alertId = json.optInt("alertId", 0),
        // optString returns "null" for a JSON null, which would render as literal text downstream.
        url = json.optString("url", "").takeIf { it.isNotEmpty() && it != "null" },
    )

    /**
     * HKDF-SHA256 (RFC 5869). Only one output block is ever needed here (32 bytes = one SHA-256 block),
     * so the expand step is a single iteration rather than the general loop.
     */
    private fun hkdfSha256(ikm: ByteArray, salt: ByteArray, info: ByteArray, length: Int): ByteArray {
        require(length <= 32) { "This HKDF only emits a single SHA-256 block" }
        val mac = Mac.getInstance("HmacSHA256")

        mac.init(SecretKeySpec(salt, "HmacSHA256"))
        val pseudoRandomKey = mac.doFinal(ikm)

        mac.init(SecretKeySpec(pseudoRandomKey, "HmacSHA256"))
        mac.update(info)
        mac.update(1.toByte())
        return mac.doFinal().copyOf(length)
    }

    private fun decodeBase64Url(value: String): ByteArray =
        Base64.decode(value, Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP)
}
