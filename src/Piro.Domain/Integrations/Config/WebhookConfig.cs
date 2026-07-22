using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Domain.Integrations.Config;

/// <summary>
/// Generic outbound webhook config (RFC 0015). The destination URL Piro POSTs/PUTs the fixed event
/// payload to, and which HTTP method to use. The auth header (phase 2) and arbitrary custom headers
/// (phase 3) are not part of v1.
/// </summary>
public sealed class WebhookConfig
{
    [Required, Url]
    [ConfigField("Endpoint URL", Placeholder = "https://hooks.zapier.com/hooks/catch/…")]
    public string Url { get; set; } = string.Empty;

    [Required]
    [ConfigFieldOptions("POST", "PUT")]
    [ConfigField("HTTP Method", HelpText = "The HTTP method used to send the payload.")]
    public string Method { get; set; } = "POST";
}
