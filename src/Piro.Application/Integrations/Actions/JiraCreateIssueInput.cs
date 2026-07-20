using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Application.Integrations.Actions;

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
    [ConfigField("Project key", Placeholder = "e.g. OPS", HelpText = "The Jira project to create the ticket in.")]
    public string ProjectKey { get; set; } = string.Empty;

    [Required]
    [ConfigField("Issue type", Placeholder = "e.g. Task")]
    public string IssueType { get; set; } = string.Empty;

    [Required]
    [ConfigField("Title", Placeholder = "Short summary of the ticket")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MultilineField]
    [ConfigField("Description", HelpText = "Markdown supported.")]
    public string Description { get; set; } = string.Empty;
}
