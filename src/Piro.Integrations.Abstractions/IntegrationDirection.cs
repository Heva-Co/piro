namespace Piro.Integrations.Abstractions;

/// <summary>
/// Which way data flows through an Integration. Orthogonal to <see cref="IntegrationCategory"/>:
/// category answers "service/action integration or notification channel?", direction answers
/// "does it receive, send, or both?" — a type can be ThirdParty and Inbound at the same time
/// (e.g. a webhook-based third-party integration).
/// </summary>
[Obsolete]
public enum IntegrationDirection
{
    Outbound,
    Inbound,
    Both,
}
