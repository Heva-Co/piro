using Piro.Contracts;

namespace Piro.Application.DTOs;

/// <summary>
/// Wire representation of a check's manifest (RFC 0011/0016) — display metadata, its minimum
/// schedule interval, the alert dimensions it supports, required integration, and its reflected
/// config-field schema. Exposed via GET /api/v1/checks/types so the admin renders the config form and
/// type picker generically, without hand-mirrored tables.
/// </summary>
public record CheckTypeMetaDto(
    string Type,
    string DisplayName,
    string Description,
    int MinIntervalSeconds,
    IReadOnlyList<CheckDimensionDto> Dimensions,
    IReadOnlyList<ConfigFieldSchemaDto> ConfigSchema,
    string? RequiredIntegrationType,
    /// <summary>
    /// False for a declared type that has no registered check implementation yet — not runnable, shown as unavailable.
    /// </summary>
    bool HasExecutor,
    /// <summary>True when this check type must run in a single region (the admin hides the multi-region toggle).</summary>
    bool SingleRegionOnly
);

/// <summary>
/// One alert dimension a check exposes, for the admin's alert form: its stable <see cref="Name"/>, how
/// it is compared (<see cref="Comparison"/>), which way is worse (<see cref="Direction"/>), and its
/// display <see cref="Unit"/>. Derived from the check's declared <c>DimensionSpec</c>.
/// </summary>
public record CheckDimensionDto(
    string Name,
    DimensionComparison Comparison,
    ThresholdDirection Direction,
    string? Unit
);
