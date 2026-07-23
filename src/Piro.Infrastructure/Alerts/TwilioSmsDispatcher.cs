using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Piro.Contracts;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert notifications as SMS via the Twilio API.</summary>
public class TwilioSmsDispatcher(ILogger<TwilioSmsDispatcher> logger, ISecretProtector secretProtector)
    : IPersonalNotificationDispatcher<AlertNotificationContext>, IVerificationCodeSender
{
    public IntegrationType Type => IntegrationType.Twilio;

    public async Task<bool> SendAsync(Integration? integration, string handle, AlertNotificationContext content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integration is null) return false;
        TwilioIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<TwilioIntegrationConfig>(integration.ReadDecryptedConfigJson(secretProtector)); }
        catch { return false; }

        TwilioClient.Init(config.AccountSid, config.AuthToken);
        var message = await MessageResource.CreateAsync(
            to: new Twilio.Types.PhoneNumber(handle),
            from: new Twilio.Types.PhoneNumber(config.FromNumber),
            body: AlertMessageTemplates.TwilioSms(content));

        logger.LogInformation("Twilio SMS personal alert sent (SID: {Sid}) to {To}.", message.Sid, handle);
        return true;
    }

    public async Task<bool> SendCodeAsync(Integration? integration, string handle, string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integration is null) return false;
        TwilioIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<TwilioIntegrationConfig>(integration.ReadDecryptedConfigJson(secretProtector)); }
        catch { return false; }

        TwilioClient.Init(config.AccountSid, config.AuthToken);
        var sms = await MessageResource.CreateAsync(
            to: new Twilio.Types.PhoneNumber(handle),
            from: new Twilio.Types.PhoneNumber(config.FromNumber),
            body: code);

        logger.LogInformation("Twilio SMS verification message sent (SID: {Sid}) to {To}.", sms.Sid, handle);
        return true;
    }

    private record TwilioIntegrationConfig(
        [property: Required] string AccountSid,
        [property: Required] string AuthToken,
        [property: Required] string FromNumber);
}
