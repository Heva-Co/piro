using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Domain.Integrations.Config;

public sealed class PagerDutyConfig
{
    [Required, SecretField]
    [ConfigField("Routing Key", Placeholder = "PagerDuty Events API v2 routing key")]
    public string RoutingKey { get; set; } = string.Empty;
}
