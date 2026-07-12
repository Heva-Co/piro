using System.Net;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Email;

/// <summary>Sends transactional emails via SMTP using MailKit.
/// Config is read from <see cref="IEmailConfigRepository"/> (DB) with fallback to <see cref="IConfiguration"/> (env vars).</summary>
public class SmtpEmailService(
    IEmailConfigRepository emailConfig,
    IConfiguration configuration,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default, string? from = null)
    {
        var config = await emailConfig.GetAsync(ct);

        // DB config takes priority; fall back to IConfiguration (env vars / appsettings)
        var smtpHost = config.SmtpHost ?? configuration["Email:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            logger.LogWarning("SMTP host is not configured. Skipping email to {To}.", to);
            return;
        }

        var port     = config.SmtpPort     ?? configuration.GetValue<int>("Email:Port", 587);
        var useTls   = config.SmtpUseTls   ?? configuration.GetValue<bool>("Email:UseSsl", true);
        var username = config.SmtpUsername ?? configuration["Email:Username"] ?? string.Empty;
        var password = config.SmtpPassword ?? configuration["Email:Password"] ?? string.Empty;
        from ??= config.SmtpFrom ?? configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(from)) from = string.IsNullOrWhiteSpace(username) ? null : username;
        if (string.IsNullOrWhiteSpace(from))
        {
            logger.LogWarning("SMTP from address is not configured. Skipping email to {To}.", to);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var body = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = body.ToMessageBody();

        // Resolve to IPv4 explicitly to avoid hanging on broken IPv6 networks.
        var addresses = await Dns.GetHostAddressesAsync(smtpHost, AddressFamily.InterNetwork, ct);
        if (addresses.Length == 0)
            throw new InvalidOperationException($"Could not resolve SMTP host '{smtpHost}' to an IPv4 address.");

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        await socket.ConnectAsync(new IPEndPoint(addresses[0], port), ct);

        using var smtp = new SmtpClient { Timeout = 15_000 };
        await smtp.ConnectAsync(socket, smtpHost, port, useTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        if (!string.IsNullOrWhiteSpace(username))
            await smtp.AuthenticateAsync(username, password, ct);
        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);

        logger.LogInformation("Email sent via SMTP to {To}: {Subject}.", to, subject);
    }

    public Task SendInvitationAsync(string to, string inviteUrl, CancellationToken ct = default) =>
        SendAsync(to, "You've been invited to Piro", EmailTemplates.Invitation(inviteUrl), ct);
}
