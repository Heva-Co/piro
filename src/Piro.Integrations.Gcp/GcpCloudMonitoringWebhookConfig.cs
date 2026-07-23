using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.Gcp;

/// <summary>
/// Configuration for the GCP Cloud Monitoring inbound webhook integration (RFC 0016). Carries the
/// single auth token GCP appends to the webhook URL as a query parameter.
/// </summary>
public sealed class GcpCloudMonitoringWebhookConfig
{
    /// <summary>
    /// Query-string auth token (RFC 0001 §4.8) — GCP's webhook notification channel only supports
    /// Basic Auth or a URL query param, no custom headers. Auto-generated on creation, never
    /// user-supplied; the core creates and HMAC-validates it.
    /// </summary>
    [Required, SecretField, GeneratedField]
    [ConfigField("Auth Token",
        HelpText = "Auto-generated. Append as ?auth_token=... to the webhook URL configured in Cloud Monitoring."
    )]
    public string AuthToken { get; set; } = string.Empty;
}
