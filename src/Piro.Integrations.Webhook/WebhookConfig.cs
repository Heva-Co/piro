using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.Webhook;

/// <summary>
/// Generic outbound webhook config (RFC 0015). The destination URL Piro POSTs/PUTs the fixed event
/// payload to, and which HTTP method to use, plus an optional auth header and arbitrary custom
/// headers. Migrated verbatim from the core's config class into this isolated integration assembly
/// (RFC 0016) — the config-field attributes come from Piro.Contracts, so it depends on no Piro.Domain
/// type.
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
