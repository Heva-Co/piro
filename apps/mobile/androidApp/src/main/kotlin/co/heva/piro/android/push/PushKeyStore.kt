package co.heva.piro.android.push

import android.content.Context
import android.util.Base64
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import java.math.BigInteger
import java.security.AlgorithmParameters
import java.security.KeyFactory
import java.security.KeyPairGenerator
import java.security.PrivateKey
import java.security.PublicKey
import java.security.interfaces.ECPublicKey
import java.security.spec.ECGenParameterSpec
import java.security.spec.ECParameterSpec
import java.security.spec.ECPoint
import java.security.spec.ECPublicKeySpec
import java.security.spec.PKCS8EncodedKeySpec

/**
 * Holds this device's push keypair. The private half never leaves the device and is never sent to any
 * backend, which is what makes a payload sealed against the public half unreadable by Piro's operator,
 * by Heva's relay, and by FCM.
 *
 * Stored in [EncryptedSharedPreferences] rather than the Android Keystore because Keystore key
 * agreement (`PURPOSE_AGREE_KEY`) requires API 31 and this app supports API 26. Refresh tokens are
 * already kept this way, and they are comparably sensitive.
 *
 * The curve is P-256 to match the backend: .NET cannot do X25519 ECDH, and P-256 is available here
 * through plain JCA on every supported API level.
 *
 * Both halves are persisted. Recovering a public key from a private scalar would mean implementing
 * curve point multiplication by hand, and hand-written curve arithmetic is not something to put in a
 * security path when storing 65 extra bytes avoids it entirely.
 */
object PushKeyStore {

    private const val PREFS_NAME = "piro_push_keys"
    private const val KEY_PRIVATE = "push_private_pkcs8"
    private const val KEY_PUBLIC = "push_public_raw"
    private const val CURVE = "secp256r1"

    /**
     * Returns the device's public key as base64url of an uncompressed EC point (65 bytes: 0x04 || X || Y),
     * generating and persisting a keypair on first call. This is the exact encoding the backend's sealer
     * expects to import.
     */
    fun publicKeyBase64Url(context: Context): String {
        ensureKeyPair(context)
        return requireNotNull(prefs(context).getString(KEY_PUBLIC, null)) {
            "Public key missing immediately after generation."
        }
    }

    /** The private key, for agreeing with the ephemeral key inside a sealed envelope. */
    fun privateKey(context: Context): PrivateKey {
        ensureKeyPair(context)
        val pkcs8 = Base64.decode(prefs(context).getString(KEY_PRIVATE, null), Base64.NO_WRAP)
        return KeyFactory.getInstance("EC").generatePrivate(PKCS8EncodedKeySpec(pkcs8))
    }

    /** True once a keypair exists, i.e. the device can receive sealed pushes. */
    fun hasKey(context: Context): Boolean {
        val p = prefs(context)
        return p.contains(KEY_PRIVATE) && p.contains(KEY_PUBLIC)
    }

    private fun ensureKeyPair(context: Context) {
        if (hasKey(context)) return

        val generator = KeyPairGenerator.getInstance("EC")
        generator.initialize(ECGenParameterSpec(CURVE))
        val pair = generator.generateKeyPair()

        val point = (pair.public as ECPublicKey).w
        val raw = ByteArray(65)
        raw[0] = 0x04
        point.affineX.toFixed32().copyInto(raw, 1)
        point.affineY.toFixed32().copyInto(raw, 33)

        // Write both halves in one commit: a private key with no matching public entry would make
        // publicKeyBase64Url throw forever.
        prefs(context).edit()
            .putString(KEY_PRIVATE, Base64.encodeToString(pair.private.encoded, Base64.NO_WRAP))
            .putString(KEY_PUBLIC, raw.toBase64Url())
            .apply()
    }

    private fun prefs(context: Context) = EncryptedSharedPreferences.create(
        context,
        PREFS_NAME,
        MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build(),
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
    )

    /** Left-pads to exactly 32 bytes, stripping BigInteger's sign byte if present. */
    private fun BigInteger.toFixed32(): ByteArray {
        val bytes = toByteArray()
        return when {
            bytes.size == 32 -> bytes
            bytes.size == 33 && bytes[0] == 0.toByte() -> bytes.copyOfRange(1, 33)
            bytes.size < 32 -> ByteArray(32 - bytes.size) + bytes
            else -> error("EC coordinate is ${bytes.size} bytes, expected at most 33")
        }
    }

    internal fun ByteArray.toBase64Url(): String =
        Base64.encodeToString(this, Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP)

    /** Rebuilds a public key from the raw uncompressed point carried in a sealed envelope. */
    internal fun publicKeyFromRawPoint(raw: ByteArray): PublicKey {
        require(raw.size == 65 && raw[0] == 0x04.toByte()) {
            "Expected a 65-byte uncompressed EC point, got ${raw.size}"
        }
        val parameters = AlgorithmParameters.getInstance("EC").apply {
            init(ECGenParameterSpec(CURVE))
        }
        val spec = parameters.getParameterSpec(ECParameterSpec::class.java)
        val x = BigInteger(1, raw.copyOfRange(1, 33))
        val y = BigInteger(1, raw.copyOfRange(33, 65))
        return KeyFactory.getInstance("EC").generatePublic(ECPublicKeySpec(ECPoint(x, y), spec))
    }
}
