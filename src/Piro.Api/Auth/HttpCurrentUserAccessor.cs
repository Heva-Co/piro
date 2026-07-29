using System.Security.Claims;
using Piro.Application.Interfaces;

namespace Piro.Api.Auth;

/// <summary>
/// Resolves the acting user from the current HTTP request's claims principal (issue #17).
/// </summary>
/// <remarks>
/// Returns null whenever there is no authenticated principal, which is what keeps machine-driven
/// writes out of the audit trail: outside a request — a Quartz job, startup seeding, the Worker —
/// there is no <c>HttpContext</c> at all, so nothing is attributable and nothing is recorded.
/// </remarks>
internal class HttpCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public CurrentUser? Current
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context?.User.Identity?.IsAuthenticated != true)
                return null;

            var principal = context.User;

            // MapInboundClaims is enabled for the JWT scheme (see Program.cs), so "sub" arrives as
            // NameIdentifier. The API-key scheme sets the same claim, so both authenticate here.
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return null;

            // Same fallback chain the controllers use when labelling a human action.
            var email = principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue("name")
                ?? string.Empty;

            return new CurrentUser(userId, email, context.Connection.RemoteIpAddress?.ToString());
        }
    }
}
