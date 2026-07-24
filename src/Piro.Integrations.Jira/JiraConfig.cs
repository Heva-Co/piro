using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.Jira;

/// <summary>
/// Jira integration config (RFC 0012). Holds the OAuth 2.0 (3LO) app credentials the admin registers
/// when creating the integration; the access/refresh tokens themselves live encrypted in the OAuth
/// token store (RFC 0004), not here. The plaintext <c>Email</c>/<c>ApiToken</c>/<c>BaseUrl</c> of the
/// old Basic-auth config are gone — superseded by the OAuth connection.
/// <para>
/// One Jira integration connects Piro to one Atlassian account and can create tickets in <b>any</b>
/// project on it — the project and issue type are chosen per ticket in the action dialog, so there is
/// no integration-per-project. <see cref="DefaultProjectKey"/>/<see cref="DefaultIssueType"/> are
/// optional connection-level defaults the dialog pre-selects; they are not required.
/// </para>
/// </summary>
public sealed class JiraConfig
{
    [Required]
    [ConfigField("Client ID", Placeholder = "Atlassian OAuth app client ID")]
    public string ClientId { get; set; } = string.Empty;

    [Required, SecretField]
    [ConfigField("Client Secret", Placeholder = "Atlassian OAuth app client secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [DynamicOptions("jira-projects")]
    [ConfigField("Default project key",
        Placeholder = "Select a default project",
        HelpText = "Optional — pre-selected when creating a ticket; can be changed per ticket.")]
    public string? DefaultProjectKey { get; set; }

    [DynamicOptions("jira-issue-types", DependsOn = nameof(DefaultProjectKey))]
    [ConfigField("Default issue type",
        Placeholder = "Select a default issue type",
        HelpText = "Optional — pre-selected when creating a ticket; can be changed per ticket.")]
    public string? DefaultIssueType { get; set; }
}
