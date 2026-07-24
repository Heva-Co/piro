using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.Jira;

/// <summary>
/// The single source of truth for the "Create Jira ticket" dialog and its execute payload (RFC 0012
/// §4.6): <c>ConfigSchemaBuilder.For(typeof(JiraCreateIssueInput))</c> renders the form, and the same
/// DataAnnotations validate the POST — so the form and the accepted payload can't drift.
/// <para>
/// Project and issue type are chosen <b>per ticket</b> (so one Jira integration serves every project),
/// pre-filled from the integration's optional defaults. Title/Description are the always-per-ticket
/// human decisions.
/// </para>
/// </summary>
public sealed class JiraCreateIssueInput
{
    [Required]
    [DynamicOptions("jira-projects")]
    [ConfigField("Project key", Placeholder = "Select a project", HelpText = "The Jira project to create the ticket in.")]
    public string ProjectKey { get; set; } = string.Empty;

    [Required]
    [DynamicOptions("jira-issue-types", DependsOn = nameof(ProjectKey))]
    [ConfigField("Issue type", Placeholder = "Select an issue type")]
    public string IssueType { get; set; } = string.Empty;

    [Required]
    [ConfigField("Title", Placeholder = "Short summary of the ticket")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MarkdownField]
    [ConfigField("Description", HelpText = "Markdown supported.")]
    public string Description { get; set; } = string.Empty;
}
