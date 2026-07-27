namespace Piro.Application.Config;

/// <summary>
/// The parsed shape of one <c>piro.yaml</c> file (RFC 0019 §4.1). Every field below the required
/// identity is nullable on purpose: null means "the file did not declare this", which the reconciler
/// reads as "leave it alone". That is the design principle in the type system — the document is a
/// partial assertion, so there is no way to express "declared" and "absent" with the same value.
/// </summary>
public sealed class ConfigDocument
{
    /// <summary>Format discriminator. Only <c>1</c> is understood; a required top-level field.</summary>
    public int? Version { get; set; }

    public List<ConfigServiceNode> Services { get; set; } = [];
}

/// <summary>A service declared in YAML, with the checks nested under it.</summary>
public sealed class ConfigServiceNode
{
    /// <summary>Required, immutable identity. Renaming it is a delete plus a create (RFC 0019 §4.2).</summary>
    public string? Slug { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsHidden { get; set; }
    public int? DisplayOrder { get; set; }

    /// <summary>Status shown before any check has reported. A <c>ServiceStatus</c> name.</summary>
    public string? DefaultStatus { get; set; }

    public List<ConfigCheckNode> Checks { get; set; } = [];

    /// <summary>1-based line of this node's first key, for error and plan attribution.</summary>
    public int Line { get; set; }
}

/// <summary>A check declared in YAML, scoped to its parent service.</summary>
public sealed class ConfigCheckNode
{
    /// <summary>Required, immutable within its service.</summary>
    public string? Slug { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Required and immutable — changing it is a replace, not an edit.</summary>
    public string? Type { get; set; }

    public string? Cron { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>
    /// The check's type-specific config as a YAML mapping, serialized to JSON before it reaches
    /// <c>Check.TypeDataJson</c>. Null when the file omitted it.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? TypeData { get; set; }

    /// <summary>
    /// Worker tags a runner must carry to execute this check, as key/value pairs matching the shared
    /// worker-tag vocabulary (e.g. <c>piro:region: eu-west</c>). A null value is a key-only flag.
    /// Rejected for a check type whose manifest sets <c>SingleRegionOnly</c>, matching
    /// <c>EnsureCanRequireWorkerTagsAsync</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? RequiredWorkerTags { get; set; }

    /// <summary>
    /// Alert rules for this check. Matched against existing rows by <see cref="ConfigAlertConfigNode.Dimension"/>,
    /// which is the closest thing to a stable identity an <c>AlertConfig</c> has — it carries no slug, and its
    /// <c>Comparison</c>/<c>Direction</c> are copied from the dimension's spec anyway. Matching in place rather
    /// than by delete-and-recreate is what preserves <c>IsAlerting</c>, so editing a threshold does not
    /// re-notify an alert that was already firing. One rule per dimension per check follows from that choice.
    /// </summary>
    public List<ConfigAlertConfigNode>? AlertConfigs { get; set; }

    /// <summary>1-based line of this node's first key, for error and plan attribution.</summary>
    public int Line { get; set; }
}

/// <summary>
/// An alert rule declared in YAML, scoped to its parent check. <c>comparison</c> and <c>direction</c>
/// are deliberately absent — both are copied from the check's declared <c>DimensionSpec</c>, so the
/// file cannot contradict the check about what its own dimension means. <c>is_alerting</c> is absent
/// because it is live state, not configuration.
/// </summary>
public sealed class ConfigAlertConfigNode
{
    /// <summary>Required. Must name a dimension the check's manifest declares.</summary>
    public string? Dimension { get; set; }

    /// <summary>Required. A ServiceStatus name for an Equality dimension, otherwise a numeric threshold.</summary>
    public string? AlertValue { get; set; }

    public int? FailureThreshold { get; set; }
    public int? SuccessThreshold { get; set; }
    public int? MinFailingRegions { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>An <c>AlertSeverity</c> name.</summary>
    public string? Severity { get; set; }

    /// <summary>1-based line of this node's first key, for error and plan attribution.</summary>
    public int Line { get; set; }
}
