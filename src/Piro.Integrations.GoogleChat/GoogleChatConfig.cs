using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.GoogleChat;

/// <summary>
/// Google Chat integration config (RFC 0009) — the incoming-webhook URL of the space to post to. The
/// URL embeds its own key, so it is treated as a secret.
/// </summary>
public sealed class GoogleChatConfig
{
    [Required, Url, SecretField]
    [ConfigField("Incoming Webhook URL", Placeholder = "https://chat.googleapis.com/v1/spaces/…/messages?key=…")]
    public string WebhookUrl { get; set; } = string.Empty;
}
