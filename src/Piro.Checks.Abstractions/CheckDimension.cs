using Piro.Contracts;

namespace Piro.Checks.Abstractions;

/// <summary>
/// One self-describing measurement a check produces (RFC 0016-style). A check reports a set of these; a
/// check with several simultaneous metrics (e.g. GCP: last-run age AND failed-task count) returns one
/// per metric. The alert policy compares <see cref="Value"/> against its own threshold for
/// <see cref="Name"/>, using <see cref="Direction"/> to know which way is worse — so the policy stays a
/// single generic evaluator with no switch on check type.
/// </summary>
public sealed record CheckDimension(
    /// <summary>Stable key matching one of the check's declared <c>CheckManifest.AlertDimensions</c> (e.g. "Latency", "CertExpiry", "FailedNameServers", "LastRunAge"). The policy binds thresholds to this.</summary>
    string Name,
    /// <summary>The measured value the policy compares against a threshold.</summary>
    double Value,
    /// <summary>Which way is worse, so the policy compares generically.</summary>
    ThresholdDirection Direction,
    /// <summary>Optional unit for display/UI ("ms", "days", "count"). Not used in comparison.</summary>
    string? Unit = null);
