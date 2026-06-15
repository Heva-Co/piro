namespace Piro.Application.Interfaces;

/// <summary>Sends transactional emails (invitations, notifications, etc.).</summary>
public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default, string? from = null);
}
