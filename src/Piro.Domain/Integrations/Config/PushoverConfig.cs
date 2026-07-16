using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Domain.Integrations.Config;

public sealed class PushoverConfig
{
    [Required, SecretField]
    [ConfigField("App Token",
        Placeholder = "axxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        HelpText = "Each user provides their own User Key when configuring a channel."
    )]
    public string AppToken { get; set; } = string.Empty;
}
