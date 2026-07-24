namespace Piro.Domain.Enums;

/// <summary>
/// Protocol or mechanism used to probe a service. The discriminator persisted on each Check row; its
/// name matches the corresponding check's <c>ICheck.CheckId</c> in the check SDK (RFC 0016), which is
/// the single source of that type's metadata, config shape, and alert dimensions. <see cref="Heartbeat"/>
/// is declared but not yet implemented — no check is registered for it.
/// </summary>
public enum CheckType
{
    HTTP,
    DNS,
    TCP,
    Ping,
    SSL,

    /// <summary>Declared but not implemented — no registered check (RFC 0011 §8).</summary>
    Heartbeat,

    GRPC,
    GCP_CloudRunJob
}
