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

    [SecretField]
    [ConfigField("Authorization header",
        Placeholder = "Bearer … or an API key",
        HelpText = "Optional. Sent as the Authorization request header, e.g. a bearer token or API key.")]
    public string? AuthorizationHeader { get; set; }

    [ConfigField("Custom headers",
        HelpText = "Optional. Extra HTTP headers sent with every request. Content-Type and Authorization are managed by Piro and cannot be overridden here.")]
    public Dictionary<string, string>? CustomHeaders { get; set; }
}
