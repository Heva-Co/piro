namespace Piro.Domain.Enums;

/// <summary>What an audit entry records (issue #17).</summary>
public enum AuditAction
{
    /// <summary>An <see cref="Piro.Domain.Auditing.IAuditable"/> entity was inserted.</summary>
    Create = 0,

    /// <summary>An <see cref="Piro.Domain.Auditing.IAuditable"/> entity was modified.</summary>
    Update = 1,

    /// <summary>An <see cref="Piro.Domain.Auditing.IAuditable"/> entity was deleted.</summary>
    Delete = 2,

    /// <summary>A user authenticated successfully. Written explicitly, not by the interceptor.</summary>
    Login = 3,

    /// <summary>An authentication attempt was rejected. Written explicitly, not by the interceptor.</summary>
    LoginFailed = 4,

    /// <summary>A user signed out. Written explicitly, not by the interceptor.</summary>
    Logout = 5,
}
