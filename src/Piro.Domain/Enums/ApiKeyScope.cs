namespace Piro.Domain.Enums;

/// <summary>
/// What an API key authorizes. <see cref="Full"/> keys authenticate as their owning user with all their
/// roles — today's behavior. <see cref="CheckInbound"/> keys are bound to a single check and authorize
/// only inbound requests to that check (e.g. a liveness ping): no roles, no user identity, so a leaked
/// URL touches one check and nothing more. Agnostic to which check type consumes it.
/// </summary>
public enum ApiKeyScope
{
    /// <summary>Full user credential — the default; authenticates as the owning user.</summary>
    Full,

    /// <summary>Bound to one check (see <c>ApiKey.CheckId</c>); authorizes only inbound requests to that check.</summary>
    CheckInbound,
}
