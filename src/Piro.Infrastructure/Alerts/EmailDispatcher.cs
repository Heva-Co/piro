using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert notifications via SMTP.</summary>
public class EmailDispatcher(
    IEmailService emailService,
    IConfiguration configuration,
    ISiteConfigRepository siteConfigRepo,
    ILogger<EmailDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Email;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<EmailChannelMeta>(channel.MetaJson);

        var subject = BuildSubject(context);
        var body = string.IsNullOrWhiteSpace(meta.Template)
            ? BuildDefaultBody(context)
            : NotificationTemplateHelper.Render(meta.Template,
                await NotificationTemplateHelper.BuildVariablesAsync(context, siteConfigRepo, configuration, ct));

        await emailService.SendAsync(meta.To, subject, body, ct, meta.From);
        logger.LogInformation("Email alert sent to {To} for {Service}/{Check}.", meta.To, context.ServiceName, context.CheckName);
    }

    public async Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        await emailService.SendAsync(handle, BuildSubject(context), BuildDefaultBody(context), ct);
        logger.LogInformation("Email personal alert sent to {To}.", handle);
        return true;
    }

    private static string BuildSubject(AlertNotificationContext ctx) =>
        ctx.IsRecovery
            ? $"[RECOVERED] {ctx.ServiceName} / {ctx.CheckName}"
            : $"[{ctx.Severity.ToString().ToUpperInvariant()}] {ctx.ServiceName} / {ctx.CheckName}";

    private static string BuildDefaultBody(AlertNotificationContext ctx) =>
        ctx.IsRecovery
            ? $"<p>✅ <strong>{ctx.CheckName}</strong> on <strong>{ctx.ServiceName}</strong> has recovered.</p>" +
              $"<p>Status: {ctx.CurrentStatus}<br>Time: {ctx.FiredAt:u}</p>"
            : $"<p>🚨 Alert fired for <strong>{ctx.CheckName}</strong> on <strong>{ctx.ServiceName}</strong>.</p>" +
              $"<p>Status: {ctx.CurrentStatus}<br>Severity: {ctx.Severity}" +
              $"{(ctx.AlertDescription is not null ? $"<br>Note: {ctx.AlertDescription}" : string.Empty)}<br>Time: {ctx.FiredAt:u}</p>";

    private record EmailChannelMeta([property: Required] string To, string? From = null, string? Template = null);
}
