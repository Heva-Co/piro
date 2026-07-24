using Piro.Contracts;

namespace Piro.Integrations.Abstractions;

/// <summary>
/// A user-initiated integration action — a button Piro renders on an Alert/Incident/Maintenance detail
/// page (RFC 0012, RFC 0016). The integration registers it imperatively at startup via
/// <see cref="IUIExtensionHost.AddAction"/>; it is not discovered by DI. The action self-describes its
/// metadata (id, label, icon, surfaces, input) and its behavior (readiness, draft, execute).
/// <para>
/// Behavior methods receive the root <see cref="IIntegrationHost"/> (for the OAuth token and the
/// integration's own config) and a fully-resolved <see cref="UIActionContext"/>. Following "Forma 1"
/// (RFC 0016): the executor resolves the target and hands it to the action in the context — the action
/// never asks Piro to read an Alert/Incident for it, and never touches a repository or the external-
/// reference table. It just returns what it created; the caller persists that.
/// </para>
/// </summary>
public interface IUIAction
{
    /// <summary>Stable integration id this action belongs to (e.g. "Jira").</summary>
    string IntegrationId { get; }

    /// <summary>Stable id, unique within an integration (e.g. "create-issue").</summary>
    string ActionId { get; }

    /// <summary>Human label for the action button (e.g. "Create Jira ticket").</summary>
    string Label { get; }

    /// <summary>One-line description shown with the button.</summary>
    string? Description { get; }

    /// <summary>Iconify icon for the button (e.g. "logos:jira").</summary>
    string? IconifyIcon { get; }

    /// <summary>The object kinds this action applies to (Alert / Incident / Maintenance).</summary>
    IReadOnlyList<UISurface> Contexts { get; }

    /// <summary>True when the action opens an input dialog (implies <see cref="InputType"/> is non-null).</summary>
    bool HasInput { get; }

    /// <summary>True when the action can pre-fill its input from the target (see <see cref="BuildDraftAsync"/>).</summary>
    bool SupportsDraft { get; }

    /// <summary>
    /// The DataAnnotations-annotated input class whose <c>ConfigSchemaBuilder.For(...)</c> schema both
    /// renders the dialog and validates the POST, or null for a no-input action.
    /// </summary>
    Type? InputType { get; }

    /// <summary>
    /// Whether the integration is ready to run this action right now (Jira: OAuth-connected). A false
    /// result drops the action from discovery entirely — no button is shown (RFC 0012 §4.4).
    /// </summary>
    Task<bool> IsReadyAsync(IIntegrationHost host, Guid integrationId, CancellationToken ct = default);

    /// <summary>Pre-fill the input for the target carried by <paramref name="ctx"/> (only called when the manifest declares <c>SupportsDraft</c>). Returns a value shaped like <see cref="InputType"/>.</summary>
    Task<object?> BuildDraftAsync(IIntegrationHost host, UIActionContext ctx, CancellationToken ct = default);

    /// <summary>Perform the action against the external system and return the reference it created. The caller (not the handler) persists it.</summary>
    Task<UIActionResult> ExecuteAsync(IIntegrationHost host, UIActionContext ctx, CancellationToken ct = default);
}
