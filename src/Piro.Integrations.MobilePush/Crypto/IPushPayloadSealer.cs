namespace Piro.Integrations.MobilePush.Crypto;

/// <summary>
/// Seals a push payload for one device. Behind an interface so the dispatcher can be tested without
/// real keys, and so the envelope implementation can be versioned independently of its callers.
/// </summary>
public interface IPushPayloadSealer
{
    /// <summary>
    /// Seals <paramref name="plaintext"/> for the device owning <paramref name="devicePublicKeyBase64Url"/>,
    /// returning the base64url envelope to put on the wire.
    /// </summary>
    /// <exception cref="ArgumentException">The public key is missing, malformed, or not a P-256 point.</exception>
    string Seal(PushPlaintext plaintext, string devicePublicKeyBase64Url);
}

/// <summary>
/// What the device actually reads after decrypting. Mirrors the fields the Android client used to read
/// from the cleartext FCM data map, so the client's rendering path is unchanged apart from the unsealing.
/// </summary>
public sealed record PushPlaintext
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string EventKey { get; init; }
    public int AlertId { get; init; }
    public string? Url { get; init; }
}
