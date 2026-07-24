namespace Piro.Integrations.Abstractions;

/// <summary>
/// Which way data flows through an Integration: does it receive, send, or both? Derived from
/// capabilities (RFC 0016) — kept only as a projected badge value. Obsolete: capabilities are the
/// source of truth and callers should read those directly.
/// </summary>
[Obsolete]
public enum IntegrationDirection
{
    Outbound,
    Inbound,
    Both,
}
