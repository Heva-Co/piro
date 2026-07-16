using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Domain.Integrations.Config;

public sealed class JiraConfig
{
    [Required, Url]
    [ConfigField("Base URL", Placeholder = "https://your-org.atlassian.net")]
    public string BaseUrl { get; set; } = string.Empty;

    [Required, EmailAddress]
    [ConfigField("Email", Placeholder = "you@example.com")]
    public string Email { get; set; } = string.Empty;

    [Required, SecretField]
    [ConfigField("API Token",
        Placeholder = "Your Jira API token",
        HelpText = "Generate one at id.atlassian.com/manage-profile/security/api-tokens"
    )]
    public string ApiToken { get; set; } = string.Empty;

    [Required]
    [ConfigField("Project Key", Placeholder = "e.g. OPS")]
    public string ProjectKey { get; set; } = string.Empty;

    [Required]
    [ConfigField("Issue Type", Placeholder = "e.g. Incident")]
    public string IssueType { get; set; } = string.Empty;
}
