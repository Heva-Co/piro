using System.Text;
using Piro.Contracts;
using Piro.Integrations.Abstractions;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Piro.Integrations.Twilio;

/// <summary>
/// Delivers a notification <see cref="Event"/> as an SMS via the Twilio SDK. Reaches Piro only through
/// <see cref="IIntegrationHost"/>: it asks the host for its decrypted <see cref="TwilioConfig"/> and
/// renders the neutral event to plain text itself. The Twilio SDK owns its own HTTP client, so this
/// dispatcher needs no HttpClient from the host. References no Piro.Domain/Infrastructure type.
/// </summary>
public sealed class TwilioNotificationDispatcher : IIntegrationEventHandler, IVerificationCodeSender
{
    public string IntegrationId => "Twilio";

    public async Task<bool> HandleAsync(Event evt, EventDeliveryContext ctx, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.Target) || ctx.IntegrationInstanceId is not { } integrationId)
            return false;

        var config = await host.GetConfigAsync<TwilioConfig>(integrationId, ct);
        if (config is null) return false;

        TwilioClient.Init(config.AccountSid, config.AuthToken);
        await MessageResource.CreateAsync(
            to: new PhoneNumber(ctx.Target),
            from: new PhoneNumber(config.FromNumber),
            body: Render(evt));
        return true;
    }

    /// <summary>Sends a one-time verification code as an SMS.</summary>
    public async Task<bool> SendCodeAsync(Guid? integrationId, string handle, string code, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integrationId is not { } id)
            return false;

        var config = await host.GetConfigAsync<TwilioConfig>(id, ct);
        if (config is null) return false;

        TwilioClient.Init(config.AccountSid, config.AuthToken);
        await MessageResource.CreateAsync(
            to: new PhoneNumber(handle),
            from: new PhoneNumber(config.FromNumber),
            body: code);
        return true;
    }

    /// <summary>Plain-text SMS body — no markup; SMS has no rich formatting.</summary>
    private static string Render(Event evt)
    {
        var sb = new StringBuilder();
        sb.Append(evt.IsResolvedLike() ? "[Resolved] " : $"[{evt.Severity}] ");
        sb.Append(evt.Title);
        if (evt is AlertEvent { Description: { } d } && !string.IsNullOrWhiteSpace(d))
            sb.Append(" — ").Append(d);
        return sb.ToString();
    }
}

internal static class EventExtensions
{
    /// <summary>True for the "resolved/recovered" event subtypes, so a dispatcher shows recovery state without an IsRecovery flag.</summary>
    public static bool IsResolvedLike(this Event evt) => evt is AlertResolvedEvent or IncidentResolvedEvent;
}
