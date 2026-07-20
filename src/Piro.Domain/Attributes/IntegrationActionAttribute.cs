using Piro.Domain.Enums;

namespace Piro.Domain.Attributes;

/// <summary>
/// Declares — at the manifest level, next to <see cref="IntegrationManifestAttribute"/> — a single
/// user-initiated action an <see cref="IntegrationType"/> contributes to the UI (RFC 0012). This
/// carries only the <b>metadata</b> of the action: its stable id, its label/icon, the object kinds
/// it applies to, and whether it takes input or can pre-fill a draft. It deliberately holds no
/// behavior — the <c>ExecuteAsync</c>/<c>BuildDraftAsync</c>/OAuth/HTTP live in an
/// <c>IIntegrationAction</c> in the Infrastructure layer, matched to this metadata by
/// <c>(IntegrationType, ActionId)</c>. Domain stays dependency-free; a manifest-honesty test asserts
/// every declared action has exactly one registered handler and vice versa.
/// <para>
/// The attribute is <b>repeatable</b>: an integration exposes as many actions as it declares, each on
/// its own <c>[IntegrationAction(...)]</c>. Several actions for the same object kind is the general
/// case, not a special one — the frontend renders one button per declared action whose
/// <see cref="Contexts"/> include the page's context.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class IntegrationActionAttribute(
    string actionId,
    string label,
    ActionContext[] contexts
) : Attribute
{
    /// <summary>Stable id, unique within an <see cref="IntegrationType"/>. Part of the route and the persisted reference (e.g. "create-issue").</summary>
    public string ActionId { get; } = actionId;

    /// <summary>Short human title for the button (e.g. "Create Jira ticket").</summary>
    public string Label { get; } = label;

    /// <summary>
    /// Longer human-readable description of what the action does — shown as the button's tooltip, the
    /// dialog's subtitle, and the entry text when several actions collapse into a menu (e.g. "Create a
    /// Jira ticket and link it to this object.").
    /// </summary>
    public string? Description { get; init; }

    /// <summary>Object kinds this action applies to — drives which pages show the button (RFC 0012 §4.3).</summary>
    public ActionContext[] Contexts { get; } = contexts;

    /// <summary>Iconify icon name for the button (same convention as <see cref="IntegrationManifestAttribute.IconifyIcon"/>, e.g. "logos:jira"). Just a string — Domain has no Iconify dependency.</summary>
    public string? IconifyIcon { get; init; }

    /// <summary>Whether the action collects human input in a dialog before executing. False = execute immediately on click (a future no-input "resync" button).</summary>
    public bool HasInput { get; init; }

    /// <summary>Whether the action can pre-fill its input dialog from the target object (RFC 0012 §4.6).</summary>
    public bool SupportsDraft { get; init; }
}
