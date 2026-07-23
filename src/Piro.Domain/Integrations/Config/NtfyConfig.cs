using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;
using Piro.Contracts;

namespace Piro.Domain.Integrations.Config;

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
