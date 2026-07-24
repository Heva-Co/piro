namespace Piro.Application.DTOs;

/// <summary>
/// What the admin shows for a check that receives inbound requests (RFC 0013): the inbound URL the target
/// calls, the masked token for reference, and when the token was last used. Returned on the check detail
/// page's inbound panel. Agnostic to the check type.
/// </summary>
/// <param name="InboundUrl">The base inbound URL for this check (no token), or null if no token exists yet.</param>
/// <param name="MaskedToken">The masked token (e.g. "ci_****abcd"), or null if none.</param>
/// <param name="LastUsedAt">When the token was last used, or null if never.</param>
public record CheckInboundTokenDto(string? InboundUrl, string? MaskedToken, DateTime? LastUsedAt);

/// <summary>The freshly rotated raw token, shown to the operator exactly once, plus its full inbound URL.</summary>
public record CheckInboundTokenRotateResultDto(string RawToken, string InboundUrl);
