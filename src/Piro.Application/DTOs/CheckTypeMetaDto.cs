using Piro.Contracts;

namespace Piro.Application.DTOs;

/// <summary>
/// Wire representation of a CheckType's manifest (RFC 0011) — display metadata, its minimum
/// schedule interval, allowed alert-fors, required integration, and its reflected config-field
/// schema. Exposed via GET /api/v1/checks/types so the admin renders the config form and type
/// picker generically, without hand-mirrored tables.
/// </summary>
public record CheckTypeMetaDto(
    string Type,
    string DisplayName,
    string Description,
    int MinIntervalSeconds,
    IReadOnlyList<string> AllowedAlertFors,
    IReadOnlyList<ConfigFieldSchemaDto> ConfigSchema,
    string? RequiredIntegrationType,
    /// <summary>
    /// False for a manifested type that has no registered executor yet (e.g. GRPC) — not runnable, shown as unavailable.
    /// </summary>
    bool HasExecutor
);
