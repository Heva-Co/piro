using System.Net;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Email;

/// <summary>Sends transactional emails via SMTP using MailKit.</summary>
public class EmailService(IConfiguration configuration, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default, string? from = null)
    {
        var smtpHost = configuration["Email:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            logger.LogWarning("SMTP host is not configured. Skipping email to {To}.", to);
            return;
        }

        var port = configuration.GetValue<int>("Email:Port", 587);
        var useSsl = configuration.GetValue<bool>("Email:UseSsl", true);
        var username = configuration["Email:Username"] ?? string.Empty;
        var password = configuration["Email:Password"] ?? string.Empty;
        from ??= configuration["Email:From"] ?? username;

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
        await smtp.ConnectAsync(socket, smtpHost, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        if (!string.IsNullOrWhiteSpace(username))
            await smtp.AuthenticateAsync(username, password, ct);
        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);

        logger.LogInformation("Email sent to {To}: {Subject}.", to, subject);
    }
}
