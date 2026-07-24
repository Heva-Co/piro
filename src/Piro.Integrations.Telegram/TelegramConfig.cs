using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.Telegram;

public sealed class TelegramConfig
{
    [Required, SecretField]
    [ConfigField("Bot Token",
        Placeholder = "123456:ABC-DEF…",
        HelpText = "From @BotFather. Each channel using this integration provides its own Chat ID."
    )]
    public string BotToken { get; set; } = string.Empty;
}
