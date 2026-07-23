using Piro.Domain.Enums;
using Piro.Contracts;

namespace Piro.Application.Integrations.Actions;

/// <summary>
/// The behavior of a user-initiated integration action — the Infrastructure-layer counterpart to the
/// declarative <see cref="Domain.Attributes.IntegrationActionAttribute"/> metadata on the
/// <see cref="IntegrationType"/> (RFC 0012). Metadata (id, label, icon, contexts, has-input/draft)
/// lives on the manifest; behavior (readiness, draft, execute) lives here; the two are matched by
/// (<see cref="Type"/>, <see cref="ActionId"/>) — the same discriminator shape the notification
/// dispatchers use.
/// <para>
/// A handler reaches the rest of Piro only through the <see cref="IActionHost"/> passed to each method
/// — never a repository or the OAuth store directly. This keeps the action a pure "given a target and
/// input, call an external system and report what it created" unit, and is what lets a future plugin
/// implement the same interface against the same host.
/// </para>
/// </summary>
public interface IIntegrationAction
{

    /// <summary>Open string discriminator (RFC 0016 §4.4), defaulted from <see cref="Type"/> during the transition; resolution moves to it in 5b.</summary>
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
    IReadOnlyList<ActionContext> Contexts { get; }

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
    /// result drops the action from discovery entirely — no button is shown (RFC 0012 §4.4). Receives
    /// the host so an action can also gate on target state (e.g. "requires an existing ticket") without
    /// touching a repository.
    /// </summary>
    Task<bool> IsReadyAsync(IActionHost host, Guid integrationId, CancellationToken ct = default);

    /// <summary>Pre-fill the input for a specific target (only called when the manifest declares <c>SupportsDraft</c>). Returns a value shaped like <see cref="InputType"/>.</summary>
    Task<object?> BuildDraftAsync(IActionHost host, ActionExecutionContext ctx, CancellationToken ct = default);

    /// <summary>Perform the action against the external system and return the reference it created. The caller (not the handler) persists it via the host.</summary>
    Task<ActionResult> ExecuteAsync(IActionHost host, ActionExecutionContext ctx, CancellationToken ct = default);
}
