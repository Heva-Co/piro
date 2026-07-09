using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert notifications as SMS via the Twilio API.</summary>
public class TwilioSmsDispatcher(ILogger<TwilioSmsDispatcher> logger) : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.TwilioSms;

    public Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<TwilioChannelMeta>(channel.MetaJson);
        var creds = ResolveCredentials(channel);

        TwilioClient.Init(creds.AccountSid, creds.AuthToken);
        var message = MessageResource.Create(
            to: new Twilio.Types.PhoneNumber(meta.ToNumber),
            from: new Twilio.Types.PhoneNumber(creds.FromNumber),
            body: BuildMessage(context));

        logger.LogInformation("Twilio SMS sent (SID: {Sid}) to {To} for {Service}/{Check}.",
            message.Sid, meta.ToNumber, context.ServiceName, context.CheckName);
        return Task.CompletedTask;
    }

    public Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return Task.FromResult(false);
        TwilioIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<TwilioIntegrationConfig>(integration.ConfigJson); }
        catch { return Task.FromResult(false); }

        TwilioClient.Init(config.AccountSid, config.AuthToken);
        var message = MessageResource.Create(
            to: new Twilio.Types.PhoneNumber(handle),
            from: new Twilio.Types.PhoneNumber(config.FromNumber),
            body: BuildMessage(context));

        logger.LogInformation("Twilio SMS personal alert sent (SID: {Sid}) to {To}.", message.Sid, handle);
        return Task.FromResult(true);
    }

    private static TwilioIntegrationConfig ResolveCredentials(NotificationChannel channel)
    {
        if (channel.Integration is not null)
        {
            var cfg = JsonUtils.DeserializeAndValidate<TwilioIntegrationConfig>(channel.Integration.ConfigJson);
            return cfg;
        }

        throw new InvalidOperationException();
    }

    private static string BuildMessage(AlertNotificationContext ctx) =>
        ctx.IsRecovery
            ? $"[Piro] RECOVERED: {ctx.ServiceName}/{ctx.CheckName} is now {ctx.CurrentStatus}."
            : $"[Piro] {ctx.Severity.ToString().ToUpperInvariant()}: {ctx.ServiceName}/{ctx.CheckName} is {ctx.CurrentStatus}. {ctx.FiredAt:u}";

    private record TwilioChannelMeta([property: Required] string ToNumber);

    private record TwilioIntegrationConfig(
        [property: Required] string AccountSid,
        [property: Required] string AuthToken,
        [property: Required] string FromNumber);
}
