using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.Ntfy;

/// <summary>
/// Configuration for the Ntfy integration (RFC 0016). Copied verbatim from the old
/// <c>Piro.Domain.Integrations.Config.NtfyConfig</c>, now carrying its config-field attributes from
/// Piro.Contracts instead of Piro.Domain so the type lives in the isolated integration assembly.
/// </summary>
public sealed class NtfyConfig
{
    [Required, Url]
    [ConfigField("Server URL",
        HelpText = "Use https://ntfy.sh for the public server or your self-hosted instance URL."
    )]
    public string ServerUrl { get; set; } = "https://ntfy.sh";

    [SecretField]
    [ConfigField("Access Token", Placeholder = "Optional — required for protected topics")]
    public string? Token { get; set; }
}
