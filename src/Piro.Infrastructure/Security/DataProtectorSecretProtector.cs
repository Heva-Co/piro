using Microsoft.AspNetCore.DataProtection;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> backed by ASP.NET Core Data Protection. Protected values carry a
/// short prefix so <see cref="IsProtected"/> can tell ciphertext from a plaintext value that predates
/// encryption (letting the same field be migrated lazily without a data migration).
/// </summary>
internal class DataProtectorSecretProtector(IDataProtectionProvider dataProtectionProvider) : ISecretProtector
{
    private const string Prefix = "prot:v1:";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Piro.ConfigSecrets.v1");

    public string Protect(string plaintext) => Prefix + _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) =>
        IsProtected(ciphertext)
            ? _protector.Unprotect(ciphertext[Prefix.Length..])
            : ciphertext; // tolerate an un-migrated plaintext value

    public bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);
}
