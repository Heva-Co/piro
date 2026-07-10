using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;

namespace Piro.Infrastructure.Email;

/// <summary>
/// Delegates to <see cref="SmtpEmailService"/> or <see cref="ResendEmailService"/> based on
/// the active provider stored in <see cref="IEmailConfigRepository"/>.
/// The provider is read on every send — queries only happen when an email is actually dispatched.
/// </summary>
public class EmailServiceFactory(
    IEmailConfigRepository emailConfig,
    SmtpEmailService smtp,
    ResendEmailService resend,
    ILogger<EmailServiceFactory> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default, string? from = null)
    {
        var config = await emailConfig.GetAsync(ct);
        var provider = config.Provider.ParseEmailProvider();

        IEmailService impl = provider switch
        {
            EmailProvider.Resend => resend,
            EmailProvider.Smtp   => smtp,
            _                    => throw new ArgumentOutOfRangeException(),
        };

        logger.LogDebug("Dispatching email via provider '{Provider}'.", provider);
        await impl.SendAsync(to, subject, htmlBody, ct, from);
    }
}
