using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class EmailConfigRepository(PiroDbContext db) : IEmailConfigRepository
{
    private const string KeyProvider = "email:provider";
    private const string KeySmtpHost = "email:smtp_host";
    private const string KeySmtpPort = "email:smtp_port";
    private const string KeySmtpUsername = "email:smtp_username";
    private const string KeySmtpPassword = "email:smtp_password";
    private const string KeySmtpFrom = "email:smtp_from";
    private const string KeySmtpUseTls = "email:smtp_use_tls";
    private const string KeyResendApiKey = "email:resend_api_key";
    private const string KeyResendFrom = "email:resend_from";

    private static readonly string[] AllKeys =
    [
        KeyProvider,
        KeySmtpHost,
        KeySmtpPort,
        KeySmtpUsername,
        KeySmtpPassword,
        KeySmtpFrom,
         KeySmtpUseTls,
        KeyResendApiKey,
        KeyResendFrom,
    ];

    public async Task<EmailProviderConfig> GetAsync(CancellationToken ct = default)
    {
        var rows = await db.SiteData
            .Where(s => AllKeys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        return new EmailProviderConfig(
            Provider: rows.GetValueOrDefault(KeyProvider),
            SmtpHost: rows.GetValueOrDefault(KeySmtpHost),
            SmtpPort: rows.TryGetValue(KeySmtpPort, out var p) && int.TryParse(p, out var port) ? port : null,
            SmtpUsername: rows.GetValueOrDefault(KeySmtpUsername),
            SmtpPassword: rows.GetValueOrDefault(KeySmtpPassword),
            SmtpFrom: rows.GetValueOrDefault(KeySmtpFrom),
            SmtpUseTls: rows.TryGetValue(KeySmtpUseTls, out var tls) ? tls == "true" : null,
            ResendApiKey: rows.GetValueOrDefault(KeyResendApiKey),
            ResendFrom: rows.GetValueOrDefault(KeyResendFrom)
        );
    }

    public async Task SetAsync(EmailProviderConfig config, CancellationToken ct = default)
    {
        var updates = new Dictionary<string, string?>
        {
            [KeyProvider] = config.Provider,
            [KeySmtpHost] = config.SmtpHost,
            [KeySmtpPort] = config.SmtpPort?.ToString(),
            [KeySmtpUsername] = config.SmtpUsername,
            [KeySmtpPassword] = config.SmtpPassword,
            [KeySmtpFrom] = config.SmtpFrom,
            [KeySmtpUseTls] = config.SmtpUseTls.HasValue ? (config.SmtpUseTls.Value ? "true" : "false") : null,
            [KeyResendApiKey] = config.ResendApiKey,
            [KeyResendFrom] = config.ResendFrom,
        };

        var existing = await db.SiteData
            .Where(s => AllKeys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, ct);

        foreach (var (key, value) in updates)
        {
            if (value is null)
            {
                if (existing.TryGetValue(key, out var row)) db.SiteData.Remove(row);
            }
            else if (existing.TryGetValue(key, out var row))
            {
                row.Value = value;
                row.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.SiteData.Add(new SiteData
                {
                    Key = key,
                    Value = value,
                    DataType = "string",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
