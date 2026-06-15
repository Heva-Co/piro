namespace Piro.Domain.Enums;

/// <summary>Protocol or mechanism used to probe a service.</summary>
public enum CheckType
{
    HTTP,
    DNS,
    TCP,
    Ping,
    SSL,
    Heartbeat,
    GRPC
}
