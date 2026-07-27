using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Piro.Integrations.MobilePush.Crypto;

namespace Piro.UnitTests;

/// <summary>
/// The envelope produced here is a compatibility contract with store-published apps, so these tests
/// pin its shape and its decryptability, not just that sealing doesn't throw. The unseal helper
/// deliberately reimplements the client side from primitives (ECDH, HKDF, AES-GCM) rather than calling
/// production code, so a change to the sealer that breaks a real client fails here instead of shipping.
/// </summary>
public class PushPayloadSealerTests
{
    private static readonly byte[] Aad = Encoding.ASCII.GetBytes("piro-push-v1");
    private static readonly byte[] HkdfInfo = Encoding.ASCII.GetBytes("piro-push-v1");

    private static PushPlaintext SamplePlaintext() => new()
    {
        Title = "CRITICAL — api.heva.co down",
        Body = "HTTP check failed: 503 in 3 regions",
        EventKey = "alert:created",
        AlertId = 4321,
        Url = "piro://alert/4321",
    };

    [Fact]
    public void Seal_RoundTripsThroughAnIndependentClientImplementation()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var plaintext = SamplePlaintext();

        var envelope = new PushPayloadSealer().Seal(plaintext, RawPublicKey(device));

        var decrypted = Unseal(envelope, device);

        Assert.Equal(plaintext.Title, decrypted.Title);
        Assert.Equal(plaintext.Body, decrypted.Body);
        Assert.Equal(plaintext.EventKey, decrypted.EventKey);
        Assert.Equal(plaintext.AlertId, decrypted.AlertId);
        Assert.Equal(plaintext.Url, decrypted.Url);
    }

    [Fact]
    public void Seal_EmitsTheDocumentedEnvelopeShape()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var envelope = new PushPayloadSealer().Seal(SamplePlaintext(), RawPublicKey(device));

        using var doc = JsonDocument.Parse(Base64Url.DecodeFromChars(envelope));
        var root = doc.RootElement;

        Assert.Equal(PushPayloadSealer.Version, root.GetProperty("V").GetInt32());
        // 65-byte uncompressed EC point, 12-byte nonce, and a ciphertext carrying the 16-byte tag.
        Assert.Equal(65, Base64Url.DecodeFromChars(root.GetProperty("Epk").GetString()!).Length);
        Assert.Equal(12, Base64Url.DecodeFromChars(root.GetProperty("N").GetString()!).Length);
        Assert.True(Base64Url.DecodeFromChars(root.GetProperty("Ct").GetString()!).Length > 16);
    }

    [Fact]
    public void Seal_UsesAFreshEphemeralKeyPerCall()
    {
        // Reusing an ephemeral key across pushes would forfeit forward secrecy, and a repeated
        // (key, nonce) pair under GCM is catastrophic rather than merely weak.
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var sealer = new PushPayloadSealer();
        var pub = RawPublicKey(device);

        var first = Epk(sealer.Seal(SamplePlaintext(), pub));
        var second = Epk(sealer.Seal(SamplePlaintext(), pub));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Seal_ProducesDifferentCiphertextForIdenticalInput()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var sealer = new PushPayloadSealer();
        var pub = RawPublicKey(device);

        var first = Ct(sealer.Seal(SamplePlaintext(), pub));
        var second = Ct(sealer.Seal(SamplePlaintext(), pub));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Seal_DoesNotLeakPlaintextIntoTheEnvelope()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var envelope = new PushPayloadSealer().Seal(SamplePlaintext(), RawPublicKey(device));

        // The whole point of the relay design: the wire bytes must not contain the alert content.
        Assert.DoesNotContain("api.heva.co", envelope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert:created", envelope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4321", envelope, StringComparison.Ordinal);
    }

    [Fact]
    public void Unseal_FailsWhenTheVersionAadIsWrong()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var envelope = new PushPayloadSealer().Seal(SamplePlaintext(), RawPublicKey(device));

        // A client that ignored the version and used different AAD must fail closed, which is what
        // makes a silent downgrade impossible.
        Assert.Throws<AuthenticationTagMismatchException>(
            () => Unseal(envelope, device, aad: Encoding.ASCII.GetBytes("piro-push-v2")));
    }

    [Fact]
    public void Unseal_FailsForADifferentDeviceKey()
    {
        using var intended = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var envelope = new PushPayloadSealer().Seal(SamplePlaintext(), RawPublicKey(intended));

        Assert.Throws<AuthenticationTagMismatchException>(() => Unseal(envelope, other));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64url!!")]
    [InlineData("c2hvcnQ")]                 // valid base64url, wrong length
    public void Seal_RejectsAMalformedPublicKey(string key)
    {
        Assert.Throws<ArgumentException>(() => new PushPayloadSealer().Seal(SamplePlaintext(), key));
    }

    [Fact]
    public void Seal_RejectsAPublicKeyThatIsNotOnTheCurve()
    {
        // Right length and right 0x04 prefix, but not a valid point — an invalid-curve attack should
        // be refused at import rather than producing a usable shared secret.
        var bogus = new byte[65];
        bogus[0] = 0x04;
        for (var i = 1; i < bogus.Length; i++) bogus[i] = 0xAA;

        Assert.Throws<ArgumentException>(
            () => new PushPayloadSealer().Seal(SamplePlaintext(), Base64Url.EncodeToString(bogus)));
    }

    [Fact]
    public void Seal_OmitsUrlWhenAbsent()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var plaintext = SamplePlaintext() with { Url = null };

        var decrypted = Unseal(new PushPayloadSealer().Seal(plaintext, RawPublicKey(device)), device);

        Assert.Null(decrypted.Url);
    }

    // --- helpers: the client side, from primitives only ---

    private static string RawPublicKey(ECDiffieHellman key)
    {
        var p = key.ExportParameters(false);
        var raw = new byte[65];
        raw[0] = 0x04;
        p.Q.X!.CopyTo(raw, 1 + (32 - p.Q.X!.Length));
        p.Q.Y!.CopyTo(raw, 33 + (32 - p.Q.Y!.Length));
        return Base64Url.EncodeToString(raw);
    }

    private static string Epk(string envelope) => Field(envelope, "Epk");
    private static string Ct(string envelope) => Field(envelope, "Ct");

    private static string Field(string envelope, string name)
    {
        using var doc = JsonDocument.Parse(Base64Url.DecodeFromChars(envelope));
        return doc.RootElement.GetProperty(name).GetString()!;
    }

    private static PushPlaintext Unseal(string envelope, ECDiffieHellman deviceKey, byte[]? aad = null)
    {
        using var doc = JsonDocument.Parse(Base64Url.DecodeFromChars(envelope));
        var root = doc.RootElement;

        var epk = Base64Url.DecodeFromChars(root.GetProperty("Epk").GetString()!);
        var nonce = Base64Url.DecodeFromChars(root.GetProperty("N").GetString()!);
        var sealedBytes = Base64Url.DecodeFromChars(root.GetProperty("Ct").GetString()!);

        var ecParams = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = epk[1..33],
                Y = epk[33..65],
            },
        };
        using var ephemeralPublic = ECDiffieHellman.Create(ecParams);

        var shared = deviceKey.DeriveRawSecretAgreement(ephemeralPublic.PublicKey);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, salt: epk, info: HkdfInfo);

        var tag = sealedBytes[^16..];
        var ciphertext = sealedBytes[..^16];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, aad ?? Aad);

        return JsonSerializer.Deserialize<PushPlaintext>(
            plaintext,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
    }
}
