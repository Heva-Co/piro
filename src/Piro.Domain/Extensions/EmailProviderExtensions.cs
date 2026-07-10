using Piro.Domain.Enums;

namespace Piro.Domain.Extensions;

public static class EmailProviderExtensions
{
    /// <summary>
    /// Parses a stored provider string (case-insensitive). Null/empty defaults to
    /// <see cref="EmailProvider.Smtp"/> (unconfigured installs have no provider saved yet).
    /// Any other unrecognized value throws — it means bad data got persisted somewhere.
    /// </summary>
    public static EmailProvider ParseEmailProvider(this string? value)
    {
        if (string.IsNullOrEmpty(value)) return EmailProvider.Smtp;

        return value.ToLowerInvariant() switch
        {
            "smtp" => EmailProvider.Smtp,
            "resend" => EmailProvider.Resend,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Expected 'smtp' or 'resend'."),
        };
    }

    /// <summary>Lowercase string used for storage/API compatibility (e.g. "smtp", "resend").</summary>
    public static string ToStorageString(this EmailProvider provider) => provider switch
    {
        EmailProvider.Smtp => "smtp",
        EmailProvider.Resend => "resend",
        _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };
}
