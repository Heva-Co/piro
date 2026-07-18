namespace Piro.Application.Interfaces;

/// <summary>
/// Encrypts/decrypts sensitive strings at rest. Application-layer abstraction over the
/// Infrastructure data-protection provider, so services can protect secrets without depending on
/// ASP.NET's IDataProtector directly.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts a plaintext value into an opaque, storable ciphertext.</summary>
    string Protect(string plaintext);

    /// <summary>Decrypts a value produced by <see cref="Protect"/>. Throws if the input isn't valid ciphertext.</summary>
    string Unprotect(string ciphertext);

    /// <summary>Returns true if the value looks like it was produced by <see cref="Protect"/> (so callers can skip double-protecting).</summary>
    bool IsProtected(string value);
}
