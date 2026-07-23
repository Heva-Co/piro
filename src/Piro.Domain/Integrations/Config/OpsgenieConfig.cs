using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;
using Piro.Contracts;

namespace Piro.Domain.Integrations.Config;

public sealed class OpsgenieConfig
{
    [Required, SecretField]
    [ConfigField("API Key", Placeholder = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx")]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [ConfigField("Region")]
    [ConfigFieldOptions("US", "EU")]
    public string Region { get; set; } = "US";
}
