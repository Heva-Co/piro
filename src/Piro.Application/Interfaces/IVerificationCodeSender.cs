using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// Delivers a one-time verification code to a personal handle when a user adds or changes a
/// notification preference. This is transactional onboarding, not alerting — the user is waiting for
/// the code — so it stays synchronous and off the async notification engine (RFC 0009 §4.9). Only
/// personal, plain-text channels (Email, Telegram, Twilio, Ntfy) implement it; a group-only channel
/// can never verify a personal handle, and the type system says so.
/// </summary>
public interface IVerificationCodeSender
{
    IntegrationType Type { get; }

    /// <summary>Open string discriminator (RFC 0016 §4.4), defaulted from <see cref="Type"/> during the transition; dispatch moves to it in 5b.</summary>
    string IntegrationId => Type.ToString();

    /// <summary>
    /// Sends a plain-text <paramref name="code"/> to <paramref name="handle"/>. Same null-integration
    /// convention as the personal dispatcher. Returns <c>true</c> if delivered; <c>false</c> otherwise.
    /// </summary>
    Task<bool> SendCodeAsync(Integration? integration, string handle, string code, CancellationToken ct = default);
}
