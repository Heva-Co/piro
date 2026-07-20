using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Application.Integrations.Actions;

/// <summary>
/// The single source of truth for the "Create Jira ticket" dialog and its execute payload (RFC 0012
/// §4.6): <c>ConfigSchemaBuilder.For(typeof(JiraCreateIssueInput))</c> renders the form, and the same
/// DataAnnotations validate the POST — so the form and the accepted payload can't drift.
/// <para>
/// Only the per-ticket human decisions live here. ProjectKey/IssueType are connection-level settings the
/// admin picks once (stored on the integration's OAuth mapping), deliberately not asked per ticket.
/// </para>
/// </summary>
public sealed class JiraCreateIssueInput
{
    [Required]
    [ConfigField("Title", Placeholder = "Short summary of the ticket")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MultilineField]
    [ConfigField("Description", HelpText = "Markdown supported.")]
    public string Description { get; set; } = string.Empty;
}
