using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;
using Piro.Contracts;

namespace Piro.Domain.Integrations.Config;

public sealed class MSTeamsConfig
{
    [Required, Url, SecretField]
    [ConfigField("Incoming Webhook URL", Placeholder = "https://outlook.office.com/webhook/…")]
    public string WebhookUrl { get; set; } = string.Empty;
}
