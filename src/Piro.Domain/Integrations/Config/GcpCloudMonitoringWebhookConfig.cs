using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Domain.Integrations.Config;

public sealed class GcpCloudMonitoringWebhookConfig
{
    /// <summary>
    /// Query-string auth token (RFC 0001 §4.8) — GCP's webhook notification channel only supports
    /// Basic Auth or a URL query param, no custom headers. Auto-generated on creation, never
    /// user-supplied (see <see cref="Services.IntegrationAppService"/>).
    /// </summary>
    [Required, SecretField, GeneratedField]
    [ConfigField("Auth Token",
        HelpText = "Auto-generated. Append as ?auth_token=... to the webhook URL configured in Cloud Monitoring."
    )]
    public string AuthToken { get; set; } = string.Empty;
}
