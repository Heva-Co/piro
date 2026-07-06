using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert notifications as SMS via the Twilio API.</summary>
public class TwilioSmsNotificationChannelDispatcher(ILogger<TwilioSmsNotificationChannelDispatcher> logger) : INotificationChannelDispatcher
{
    public IntegrationType Type => IntegrationType.TwilioSms;

    public Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Deserialize<TwilioSmsTriggerMeta>(channel.MetaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid Twilio SMS trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.AccountSid))
            throw new InvalidOperationException("Twilio Account SID is required.");
        if (string.IsNullOrWhiteSpace(meta.AuthToken))
            throw new InvalidOperationException("Twilio Auth Token is required.");
        if (string.IsNullOrWhiteSpace(meta.FromNumber))
            throw new InvalidOperationException("Twilio From Number is required.");
        if (string.IsNullOrWhiteSpace(meta.ToNumber))
            throw new InvalidOperationException("Twilio To Number is required.");

        TwilioClient.Init(meta.AccountSid, meta.AuthToken);

        var body = BuildMessage(context);

        var message = MessageResource.Create(
            to: new Twilio.Types.PhoneNumber(meta.ToNumber),
            from: new Twilio.Types.PhoneNumber(meta.FromNumber),
            body: body
        );

        logger.LogInformation("Twilio SMS sent (SID: {Sid}) to {To} for {Service}/{Check}.",
            message.Sid, meta.ToNumber, context.ServiceName, context.CheckName);

        return Task.CompletedTask;
    }

    private static string BuildMessage(AlertNotificationContext ctx)
    {
        // Keep within 160 chars for a single SMS segment
        if (ctx.IsRecovery)
            return $"[Piro] RECOVERED: {ctx.ServiceName}/{ctx.CheckName} is now {ctx.CurrentStatus}.";

        return $"[Piro] {ctx.Severity.ToString().ToUpperInvariant()}: {ctx.ServiceName}/{ctx.CheckName} is {ctx.CurrentStatus}. {ctx.FiredAt:u}";
    }

    private record TwilioSmsTriggerMeta(string AccountSid, string AuthToken, string FromNumber, string ToNumber);
}
