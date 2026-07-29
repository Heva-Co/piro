namespace Piro.Application.Interfaces;

/// <summary>
/// Resolves who is behind the current operation, for code that needs the acting user but sits
/// below the web layer (the audit interceptor, issue #17).
/// </summary>
/// <remarks>
/// Infrastructure has no access to <c>HttpContext</c>, and reaching for it there would couple
/// persistence to ASP.NET. This interface is the seam: the API implements it over
/// <c>IHttpContextAccessor</c>, and any host without a request pipeline — the Worker, Quartz jobs,
/// migrations, seeding — simply resolves nothing.
/// </remarks>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// The acting user, or null when the operation has no authenticated human behind it (a
    /// background job, a startup task, or an unauthenticated request). Callers must treat null as
    /// "not attributable" rather than substituting a placeholder identity.
    /// </summary>
    CurrentUser? Current { get; }
}

/// <summary>The identity and origin of the human behind the current operation.</summary>
/// <param name="UserId">Stable user id from the token's subject claim.</param>
/// <param name="Email">
/// The user's email, or a best-effort display name when no email claim is present. Recorded
/// alongside the id so an audit entry stays readable after the account is gone.
/// </param>
/// <param name="IpAddress">
/// Caller IP, or null when it cannot be determined. Behind a reverse proxy this is only the real
/// client address if forwarded headers are configured, otherwise it is the proxy's.
/// </param>
public record CurrentUser(string UserId, string Email, string? IpAddress);
