using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.Twilio;

public sealed class TwilioConfig
{
    [Required]
    [ConfigField("Account SID", Placeholder = "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public string AccountSid { get; set; } = string.Empty;

    [Required, SecretField]
    [ConfigField("Auth Token")]
    public string AuthToken { get; set; } = string.Empty;

    [Required]
    [ConfigField("From number", Placeholder = "+15005550006")]
    public string FromNumber { get; set; } = string.Empty;
}
