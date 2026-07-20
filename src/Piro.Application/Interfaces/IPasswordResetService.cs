namespace Piro.Application.Interfaces;

/// <summary>
/// Self-service password recovery. Mirrors the invitation flow's token mechanics
/// (custom-purpose Identity token + stored expiry in AspNetUserTokens + security-stamp
/// single-use), but targets an existing active user and is enumeration-safe.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Starts recovery for the given email. Always returns normally so the caller can
    /// answer with a uniform 200 regardless of whether the email matched. No-op when
    /// SSO-only mode is on, the email is unknown, or the account is SSO/inactive.
    /// </summary>
    Task RequestResetAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Completes recovery. Throws <see cref="InvalidOperationException"/> with a generic
    /// "Invalid or expired reset link." message on any failure (unknown user, bad/expired
    /// token, SSO/inactive account, or SSO-only mode turned on after the link was mailed).
    /// </summary>
    Task ResetAsync(int userId, string token, string newPassword, CancellationToken ct = default);
}
