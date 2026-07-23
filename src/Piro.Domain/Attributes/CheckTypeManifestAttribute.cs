using Piro.Domain.Enums;
using Piro.Contracts;

namespace Piro.Domain.Attributes;

/// <summary>
/// Declares everything known about a <see cref="CheckType"/> in one place: its display name and
/// description, its minimum schedule interval, the <see cref="AlertFor"/> values it supports, its
/// config shape (a *CheckConfig record), and any required provider Integration. Mirrors
/// <see cref="IntegrationManifestAttribute"/> for checks — consolidates what used to be scattered
/// across the enum, <c>CheckTypeExtensions.AllowedAlertFors</c>, <c>[RequiresIntegration]</c> on the
/// executor, and the frontend's hand-mirrored label/default tables (RFC 0011).
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class CheckTypeManifestAttribute(
    string displayName,
    string description,
    Type configType,
    int minIntervalSeconds,
    AlertFor[] allowedAlertFors
) : Attribute
{
    /// <summary>Human-readable name for the admin check-type picker (e.g. "GCP Cloud Run Job").</summary>
    public string DisplayName { get; } = displayName;

    /// <summary>Short description of what this check type does, shown in the picker.</summary>
    public string Description { get; } = description;

    /// <summary>The *CheckConfig record describing this type's TypeDataJson shape — reflected into the config schema (see ConfigSchemaBuilder).</summary>
    public Type ConfigType { get; } = configType;

    /// <summary>Minimum allowed interval between runs, in seconds. A Script check floors higher (arbitrary code); ordinary checks at 60.</summary>
    public int MinIntervalSeconds { get; } = minIntervalSeconds;

    /// <summary>The <see cref="AlertFor"/> values that make sense for this type — see RFC 0002 §4.4.</summary>
    public AlertFor[] AllowedAlertFors { get; } = allowedAlertFors;

    private IntegrationType _requiredIntegration;
    private bool _hasRequiredIntegration;

    /// <summary>
    /// A provider Integration this type requires before it can be used (e.g. GoogleCloud for a
    /// Cloud Run Job check). Replaces the <c>[RequiresIntegration]</c> attribute that previously
    /// lived on the executor. Set via the named argument; read as a nullable through
    /// <see cref="RequiredIntegration"/>. (Attribute named args can't be a nullable enum, hence the
    /// backing-field + presence-flag pattern.)
    /// </summary>
    public IntegrationType RequiresIntegration
    {
        get => _requiredIntegration;
        set { _requiredIntegration = value; _hasRequiredIntegration = true; }
    }

    /// <summary>The required provider Integration, or null when this type needs none.</summary>
    public IntegrationType? RequiredIntegration => _hasRequiredIntegration ? _requiredIntegration : null;
}
