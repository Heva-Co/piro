namespace Piro.Application.DTOs;

/// <summary>
/// One YAML source file in a config-as-code request, kept tagged with the path it came from
/// (RFC 0019 §4.6). Multi-file input is concatenated into one logical document, but file identity
/// survives to the validator so an error reports the file the user actually wrote — not an offset
/// into an anonymous merge they never saw.
/// </summary>
/// <param name="Path">
/// Informational only: used to attribute errors and changes back to a file, echoed back in
/// responses, and never interpreted as a filesystem path server-side. Treated as untrusted text.
/// </param>
public record ConfigDocumentSource(string Path, string Content);

/// <summary>Request body for <c>POST /api/v1/config/plan</c> and <c>/apply</c>.</summary>
/// <param name="Prune">
/// When true, resources present in Piro but absent from the documents are deleted. Off by default:
/// the YAML is a partial assertion, so silence about a resource means "leave it alone" (RFC 0019 §4.5).
/// </param>
public record ConfigApplyRequest(IReadOnlyList<ConfigDocumentSource> Documents, bool Prune = false);

/// <summary>What the reconciler will do (or did) to one resource.</summary>
public enum ConfigChangeAction
{
    /// <summary>The resource does not exist in Piro and will be created.</summary>
    Create,

    /// <summary>The resource exists and one or more declared fields differ.</summary>
    Update,

    /// <summary>The resource exists in Piro, is absent from the documents, and pruning was requested.</summary>
    Delete,

    /// <summary>The resource exists and every declared field already matches.</summary>
    NoOp,
}

/// <summary>Which kind of resource a change applies to.</summary>
public enum ConfigResourceKind
{
    Service,
    Check,

    /// <summary>
    /// An alert rule. Identified by its dimension within its check, so <c>Slug</c> carries the
    /// dimension name and <c>ParentSlug</c> the <c>service/check</c> path.
    /// </summary>
    AlertConfig,
}

/// <summary>One field changing on an update, so the plan can be rendered as a readable diff.</summary>
public record ConfigFieldChange(string Field, string? Before, string? After);

/// <summary>
/// A single planned or applied change. Flat rather than nested so a CLI can render it as a list and
/// CI can diff it, with <see cref="ParentSlug"/> carrying the service a check belongs to.
/// </summary>
/// <param name="Path">The source file that declared this resource; null for a prune-driven deletion,
/// which by definition appears in no file.</param>
/// <param name="Warnings">
/// Consequences a user must see before approving — notably that deleting a check discards its entire
/// measurement history, and that a slug or type change is a delete plus a create, not a rename
/// (RFC 0019 §4.2, §8).
/// </param>
public record ConfigResourceChange(
    ConfigResourceKind Kind,
    ConfigChangeAction Action,
    string Slug,
    string? ParentSlug = null,
    string? Path = null,
    int? Line = null,
    IReadOnlyList<ConfigFieldChange>? Fields = null,
    IReadOnlyList<string>? Warnings = null
);

/// <summary>
/// A validation failure located in the source file that caused it. Every error carries its origin so
/// a directory of twenty files produces messages a user can navigate; the reconciler collects all of
/// them rather than failing on the first (RFC 0019 §4.3).
/// </summary>
public record ConfigValidationError(
    string Message,
    string? Path = null,
    int? Line = null,
    int? Column = null,
    string? Pointer = null
);

/// <summary>Counts by action, so a CLI can print a one-line summary without walking the change list.</summary>
public record ConfigPlanSummary(int Create, int Update, int Delete, int NoOp, int Untouched);

/// <summary>
/// The result of a plan or an apply. <see cref="Applied"/> distinguishes the two, so a caller
/// reading a stored response cannot mistake a preview for a completed write.
/// </summary>
/// <param name="Untouched">
/// Resources that exist in Piro, are absent from the documents, and were left alone because pruning
/// was not requested. Reported as a count so adopting config as code for part of a topology visibly
/// does nothing to the rest.
/// </param>
/// <param name="SchedulingErrors">
/// Failures reconciling Quartz triggers after the commit. Scheduling happens outside the transaction
/// (the existing pattern), so these are surfaced rather than swallowed and the CLI can exit non-zero
/// on a partially-scheduled apply (RFC 0019 §8).
/// </param>
public record ConfigPlanDto(
    bool Applied,
    ConfigPlanSummary Summary,
    IReadOnlyList<ConfigResourceChange> Changes,
    IReadOnlyList<ConfigValidationError> Errors,
    IReadOnlyList<string> Untouched,
    IReadOnlyList<string> SchedulingErrors
);
