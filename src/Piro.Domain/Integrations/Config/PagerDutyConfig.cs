using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;
using Piro.Contracts;

namespace Piro.Domain.Integrations.Config;

/// <summary>
/// PagerDuty integration config (RFC 0004). Holds the OAuth app credentials the admin registers when
/// creating the integration. The routing keys used to actually send events are NOT here — they are
/// discovered per PagerDuty service via the REST API after connecting (a later phase) and stored in
/// the service-mapping, not in this config.
/// <para>
/// <see cref="ClientSecret"/> is masked on the way out (<c>SecretField</c>) and, for PagerDuty
/// specifically, encrypted at rest via <c>IDataProtector</c> when the OAuth flow reads it — it is an
/// app credential, not a throwaway value.
/// </para>
/// </summary>
public sealed class PagerDutyConfig
{
    [Required]
    [ConfigField("Client ID", Placeholder = "PagerDuty OAuth app client ID")]
    public string ClientId { get; set; } = string.Empty;

    [Required, SecretField]
    [ConfigField("Client Secret", Placeholder = "PagerDuty OAuth app client secret")]
    public string ClientSecret { get; set; } = string.Empty;
}
