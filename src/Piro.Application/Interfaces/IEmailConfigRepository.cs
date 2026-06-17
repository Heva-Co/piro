namespace Piro.Application.Interfaces;

/// <summary>Persists email provider configuration in SiteData.</summary>
public interface IEmailConfigRepository
{
    Task<EmailProviderConfig> GetAsync(CancellationToken ct = default);
    Task SetAsync(EmailProviderConfig config, CancellationToken ct = default);
}

public record EmailProviderConfig(
    string? Provider,
    string? SmtpHost,
    int?    SmtpPort,
    string? SmtpUsername,
    string? SmtpPassword,
    string? SmtpFrom,
    bool?   SmtpUseTls,
    string? ResendApiKey,
    string? ResendFrom
);
