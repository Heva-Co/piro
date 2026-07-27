using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Piro.Integrations.MobilePush.Crypto;

/// <summary>
/// Seals a rendered push for exactly one device, so no transport — the Heva relay included — can read
/// it. The device generates an EC keypair at registration and publishes only the public half; the
/// private half never leaves the device, so there is deliberately no recovery path.
///
/// The envelope is versioned in its first field because once a store-published app decrypts v1, the
/// format is a compatibility contract with binaries that cannot be recalled. The version is also bound
/// into the AES-GCM additional authenticated data, so a downgrade attempt fails to decrypt rather than
/// being silently reinterpreted.
///
/// The curve is NIST P-256, not X25519. .NET's <see cref="ECDiffieHellman"/> throws
/// PlatformNotSupportedException for curve25519, and hand-rolling curve arithmetic in a security path is
/// not worth the ideological win — P-256 ECDH is available in the BCL with no new dependency, in Android's
/// JCA well below our minSdk of 26, and in CryptoKit on iOS.
/// </summary>
public sealed class PushPayloadSealer : IPushPayloadSealer
{
    /// <summary>Envelope version. Bump only alongside a client that understands the new shape.</summary>
    public const int Version = 1;

    /// <summary>Bound into the GCM tag as additional authenticated data, pinning the version.</summary>
    private static readonly byte[] AssociatedData = Encoding.ASCII.GetBytes("piro-push-v1");

    /// <summary>HKDF info string, domain-separating this key from any other use of the same secret.</summary>
    private static readonly byte[] HkdfInfo = Encoding.ASCII.GetBytes("piro-push-v1");

    /// <summary>
    /// DER prefix for a prime256v1 SubjectPublicKeyInfo. The clients hand us a raw uncompressed EC point
    /// (the natural export on both platforms); .NET only imports SPKI, so we re-wrap it here rather than
    /// making every client emit DER.
    /// </summary>
    private static readonly byte[] P256SpkiPrefix =
        Convert.FromHexString("3059301306072a8648ce3d020106082a8648ce3d030107034200");

    /// <summary>Uncompressed EC point: 0x04 || X(32) || Y(32).</summary>
    private const int PublicKeyLength = 65;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32;

    private static readonly JsonSerializerOptions PlaintextJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public string Seal(PushPlaintext plaintext, string devicePublicKeyBase64Url)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        using var devicePublic = ImportDevicePublicKey(devicePublicKeyBase64Url);
        var payload = JsonSerializer.SerializeToUtf8Bytes(plaintext, PlaintextJson);

        // A fresh ephemeral keypair per push is what gives forward secrecy: recovering the device's
        // long-term private key later does not decrypt pushes captured before the compromise.
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var sharedSecret = ephemeral.DeriveRawSecretAgreement(devicePublic.PublicKey);
        var ephemeralPublic = ExportRawPublicKey(ephemeral);

        try
        {
            // Salt with the ephemeral public key so two pushes to the same device never derive the same
            // content key, even if the shared secret were somehow repeated.
            var key = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: sharedSecret,
                outputLength: KeyLength,
                salt: ephemeralPublic,
                info: HkdfInfo);

            try
            {
                var nonce = RandomNumberGenerator.GetBytes(NonceLength);
                var ciphertext = new byte[payload.Length];
                var tag = new byte[TagLength];

                using var aes = new AesGcm(key, TagLength);
                aes.Encrypt(nonce, payload, ciphertext, tag, AssociatedData);

                // Tag appended to the ciphertext, which is the layout both platform AEAD APIs expect.
                var sealedBytes = new byte[ciphertext.Length + TagLength];
                ciphertext.CopyTo(sealedBytes, 0);
                tag.CopyTo(sealedBytes, ciphertext.Length);

                var envelope = new SealedEnvelope
                {
                    V = Version,
                    Epk = Base64Url.EncodeToString(ephemeralPublic),
                    N = Base64Url.EncodeToString(nonce),
                    Ct = Base64Url.EncodeToString(sealedBytes),
                };

                return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(envelope));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static ECDiffieHellman ImportDevicePublicKey(string base64Url)
    {
        if (string.IsNullOrWhiteSpace(base64Url))
            throw new ArgumentException("Device push public key is missing.", nameof(base64Url));

        byte[] raw;
        try
        {
            raw = Base64Url.DecodeFromChars(base64Url);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Device push public key is not valid base64url.", nameof(base64Url), ex);
        }

        if (raw.Length != PublicKeyLength || raw[0] != 0x04)
        {
            throw new ArgumentException(
                $"Device push public key must be a {PublicKeyLength}-byte uncompressed EC point, got {raw.Length} bytes.",
                nameof(base64Url));
        }

        var spki = new byte[P256SpkiPrefix.Length + raw.Length];
        P256SpkiPrefix.CopyTo(spki, 0);
        raw.CopyTo(spki, P256SpkiPrefix.Length);

        var ecdh = ECDiffieHellman.Create();
        try
        {
            ecdh.ImportSubjectPublicKeyInfo(spki, out _);
            return ecdh;
        }
        catch (CryptographicException ex)
        {
            ecdh.Dispose();
            throw new ArgumentException("Device push public key is not a valid P-256 point.", nameof(base64Url), ex);
        }
    }

    private static byte[] ExportRawPublicKey(ECDiffieHellman ecdh)
    {
        var p = ecdh.ExportParameters(false);
        var x = p.Q.X ?? throw new CryptographicException("Ephemeral key has no X coordinate.");
        var y = p.Q.Y ?? throw new CryptographicException("Ephemeral key has no Y coordinate.");

        // Left-pad each coordinate to 32 bytes: the BCL trims leading zeros, and a short coordinate
        // would silently shift the point and break agreement on the client.
        var raw = new byte[PublicKeyLength];
        raw[0] = 0x04;
        x.CopyTo(raw, 1 + (32 - x.Length));
        y.CopyTo(raw, 33 + (32 - y.Length));
        return raw;
    }

    /// <summary>The wire envelope. Short field names keep it small — an FCM data message caps at 4 KiB.</summary>
    private sealed class SealedEnvelope
    {
        public int V { get; set; }
        public string Epk { get; set; } = string.Empty;
        public string N { get; set; } = string.Empty;
        public string Ct { get; set; } = string.Empty;
    }
}
