namespace Piro.Application.Interfaces;

/// <summary>Sends transactional emails (invitations, notifications, etc.).</summary>
public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default, string? from = null);

    /// <summary>
    /// Sends the user-invitation email. The HTML template lives in Infrastructure — Application
    /// only knows it needs an invitation sent, not how the email is rendered.
    /// </summary>
    Task SendInvitationAsync(string to, string inviteUrl, CancellationToken ct = default);

    /// <summary>
    /// Sends the password-reset email. The HTML template lives in Infrastructure — Application
    /// only knows it needs a reset link sent, not how the email is rendered.
    /// </summary>
    Task SendPasswordResetAsync(string to, string resetUrl, CancellationToken ct = default);
}
