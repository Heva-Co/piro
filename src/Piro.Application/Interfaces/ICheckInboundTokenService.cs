namespace Piro.Application.Interfaces;

/// <summary>
/// Issues and inspects the scoped inbound token for a check that receives inbound requests (RFC 0013).
/// An Application-level seam over the API-key store so <see cref="Services.CheckAppService"/> can mint a
/// token when such a check is created and surface it (masked, plus last-used time) on the check, without
/// depending on the Infrastructure auth implementation. Agnostic to the check type — any check that ships
/// an inbound handler uses it.
/// </summary>
public interface ICheckInboundTokenService
{
    /// <summary>
    /// Creates (or rotates) the inbound token for a check and returns the raw token, shown once.
    /// </summary>
    Task<string> CreateOrRotateAsync(int checkId, CancellationToken ct = default);

    /// <summary>
    /// The masked token and last-used time for a check, or null if it has no inbound token.
    /// </summary>
    Task<CheckInboundTokenInfo?> GetInfoAsync(int checkId, CancellationToken ct = default);
}

/// <summary>Read-back info for a check's inbound token: the masked form and when it was last used.</summary>
public record CheckInboundTokenInfo(string MaskedToken, DateTime? LastUsedAt);
