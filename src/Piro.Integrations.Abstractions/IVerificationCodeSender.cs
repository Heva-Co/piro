namespace Piro.Integrations.Abstractions;

/// <summary>
/// Delivers a one-time verification code to a personal handle when a user adds or changes a personal
/// notification preference (RFC 0009 §4.9, RFC 0016). Transactional onboarding, not alerting — the user
/// is waiting for the code — so it stays synchronous, off the async notification engine.
/// <para>
/// Only personal channels implement it (an integration that declares
/// <see cref="IntegrationCapability.SendsPersonalNotification"/>); it lives in the same assembly as that
/// integration's notification handler and reaches Piro only through <see cref="IIntegrationHost"/> — it
/// never sees Piro's <c>Integration</c> entity or a repository.
/// </para>
/// </summary>
public interface IVerificationCodeSender
{
    /// <summary>The integration id this sender belongs to (e.g. "Telegram").</summary>
    string IntegrationId { get; }

    /// <summary>
    /// Sends a plain-text <paramref name="code"/> to <paramref name="handle"/> for the given integration
    /// instance, reading its config/credentials through <paramref name="host"/>. Returns true if
    /// delivered. <paramref name="integrationId"/> is null for a platform-wide channel with no instance
    /// row (e.g. Email); such senders read their transport from configuration, not the host.
    /// </summary>
    Task<bool> SendCodeAsync(Guid? integrationId, string handle, string code, IIntegrationHost host, CancellationToken ct = default);
}
