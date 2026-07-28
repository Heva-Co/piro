using System.Text.Json.Serialization;

namespace Piro.Cli;

/// <summary>
/// The CLI's own copy of the request and response shapes it exchanges with the API.
/// </summary>
/// <remarks>
/// Deliberately duplicated rather than shared with <c>Piro.Application</c>: that assembly transitively
/// pulls ASP.NET Core and EF Core, which NativeAOT cannot handle (RFC 0019 §4.6). The surface is small
/// and additive-only — a field the server adds and the CLI does not know about is simply ignored —
/// so the duplication costs little and keeps the binary self-contained.
/// </remarks>
internal sealed record ConfigDocumentSource(string Path, string Content);

internal sealed record ConfigApplyRequest(IReadOnlyList<ConfigDocumentSource> Documents, bool Prune);

/// <summary>Mirrors the server's enum by name; unknown values deserialize via the string converter.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConfigChangeAction>))]
internal enum ConfigChangeAction
{
    Create,
    Update,
    Delete,
    NoOp,
}

[JsonConverter(typeof(JsonStringEnumConverter<ConfigResourceKind>))]
internal enum ConfigResourceKind
{
    Service,
    Check,
    AlertConfig,
}

internal sealed record ConfigFieldChange(string Field, string? Before, string? After);

internal sealed record ConfigResourceChange(
    ConfigResourceKind Kind,
    ConfigChangeAction Action,
    string Slug,
    string? ParentSlug,
    string? Path,
    int? Line,
    IReadOnlyList<ConfigFieldChange>? Fields,
    IReadOnlyList<string>? Warnings);

internal sealed record ConfigValidationError(
    string Message,
    string? Path,
    int? Line,
    int? Column,
    string? Pointer);

internal sealed record ConfigPlanSummary(int Create, int Update, int Delete, int NoOp, int Untouched);

internal sealed record ConfigPlanDto(
    bool Applied,
    ConfigPlanSummary Summary,
    IReadOnlyList<ConfigResourceChange> Changes,
    IReadOnlyList<ConfigValidationError> Errors,
    IReadOnlyList<string> Untouched,
    IReadOnlyList<string> SchedulingErrors);

/// <summary>Identity of the caller, printed before a plan or apply so the target is never a surprise.</summary>
internal sealed record CurrentUserDto(string? Email, string? Name);

/// <summary>
/// Source-generated serialization for every type crossing the wire. Required: reflection-based
/// serialization is disabled in this project so the AOT binary carries no serializer metadata it
/// cannot see at compile time.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ConfigApplyRequest))]
[JsonSerializable(typeof(ConfigPlanDto))]
[JsonSerializable(typeof(CurrentUserDto))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
