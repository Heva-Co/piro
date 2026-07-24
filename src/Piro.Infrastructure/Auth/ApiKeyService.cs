using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Auth;

/// <summary>Creates, lists, and validates API keys.</summary>
/// <remarks>
/// Raw keys are never stored. Only the SHA-256 hash is persisted — this is safe without
/// a per-key salt/pepper because the raw key itself is 32 cryptographically random bytes
/// (not a low-entropy secret like a password), so it isn't vulnerable to precomputed
/// rainbow-table attacks the way a salted password hash would need to defend against.
/// Format: <c>pk_{32 random hex bytes}</c>.
/// </remarks>
public class ApiKeyService(PiroDbContext db)
{
    public async Task<ApiKeyCreatedResponse> CreateAsync(int userId, CreateApiKeyRequest request, CancellationToken ct = default)
    {
        var rawKey = $"pk_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower()}";
        var hashed = Hash(rawKey);
        var masked = $"pk_****{rawKey[^4..]}";

        var entity = new ApiKey
        {
            UserId = userId,
            Name = request.Name,
            HashedKey = hashed,
            MaskedKey = masked,
            Status = ApiKeyStatus.Active
        };

        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);

        return new ApiKeyCreatedResponse(entity.Id, entity.Name, rawKey, masked, entity.CreatedAt);
    }

    public async Task<IEnumerable<ApiKeyDto>> GetByUserAsync(int userId, CancellationToken ct = default)
    {
        var keys = await db.ApiKeys
            .Where(k => k.UserId == userId && k.Status == ApiKeyStatus.Active)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return keys.Select(k => new ApiKeyDto(k.Id, k.Name, k.MaskedKey, k.Status, k.CreatedAt, k.LastUsedAt));
    }

    /// <summary>Revokes an active API key. Throws if the key doesn't exist or is already revoked.</summary>
    public async Task RevokeAsync(int keyId, int userId, CancellationToken ct = default)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct)
            ?? throw new NotFoundException("ApiKey", keyId);

        if (key.Status == ApiKeyStatus.Revoked)
            throw new DomainValidationException("API key is already revoked.");

        key.Status = ApiKeyStatus.Revoked;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Validates a raw API key and returns the owning user ID, or null if invalid. Updates LastUsedAt on success.</summary>
    /// <remarks>
    /// Constrained to <see cref="ApiKeyScope.Full"/> so a Heartbeat-scoped token can never authenticate a
    /// normal API request even if placed in an <c>X-Api-Key</c> header — it carries no user identity or
    /// roles and must only ever ping its bound check (RFC 0013).
    /// </remarks>
    public async Task<int?> ValidateAsync(string rawKey, CancellationToken ct = default)
    {
        var hashed = Hash(rawKey);
        // Constrained to Full so a check-inbound token can never authenticate a normal API request.
        var key = await db.ApiKeys.FirstOrDefaultAsync(
            k => k.HashedKey == hashed && k.Status == ApiKeyStatus.Active && k.Scope == ApiKeyScope.Full, ct);
        if (key is null)
            return null;

        key.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return key.UserId;
    }

    /// <summary>
    /// Creates a CheckInbound-scoped token bound to <paramref name="checkId"/> (RFC 0013): no user, no
    /// roles, only inbound requests to that check. Format <c>ci_{64 hex}</c> so it's visually distinct
    /// from a <c>pk_</c> user key. The raw token is returned once. Any existing active inbound key for the
    /// check is revoked first, so this doubles as "rotate". Agnostic to check type.
    /// </summary>
    public async Task<string> CreateOrRotateCheckInboundKeyAsync(int checkId, CancellationToken ct = default)
    {
        var existing = await db.ApiKeys
            .Where(k => k.CheckId == checkId && k.Scope == ApiKeyScope.CheckInbound && k.Status == ApiKeyStatus.Active)
            .ToListAsync(ct);
        foreach (var k in existing)
            k.Status = ApiKeyStatus.Revoked;

        var rawKey = $"ci_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower()}";
        db.ApiKeys.Add(new ApiKey
        {
            UserId = null,
            Name = $"check-inbound-{checkId}-{Guid.NewGuid():N}",
            HashedKey = Hash(rawKey),
            MaskedKey = $"ci_****{rawKey[^4..]}",
            Status = ApiKeyStatus.Active,
            Scope = ApiKeyScope.CheckInbound,
            CheckId = checkId,
        });
        await db.SaveChangesAsync(ct);
        return rawKey;
    }

    /// <summary>
    /// Validates a check-inbound token against a specific check (RFC 0013): matches on hash, Active,
    /// Scope=CheckInbound, AND CheckId. Constant-time hash comparison; never touches user identity.
    /// Updates LastUsedAt on success. Returns true iff the token is a live inbound key for that check.
    /// </summary>
    public async Task<bool> ValidateCheckInboundAsync(string rawToken, int checkId, CancellationToken ct = default)
    {
        var hashed = Hash(rawToken);
        var key = await db.ApiKeys.FirstOrDefaultAsync(
            k => k.CheckId == checkId && k.Scope == ApiKeyScope.CheckInbound && k.Status == ApiKeyStatus.Active, ct);
        if (key is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(key.HashedKey), Encoding.UTF8.GetBytes(hashed)))
            return false;

        key.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>The active check-inbound key's masked form + last-used time for a check, or null if none.</summary>
    public async Task<(string MaskedKey, DateTime? LastUsedAt)?> GetCheckInboundKeyInfoAsync(int checkId, CancellationToken ct = default)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(
            k => k.CheckId == checkId && k.Scope == ApiKeyScope.CheckInbound && k.Status == ApiKeyStatus.Active, ct);
        return key is null ? null : (key.MaskedKey, key.LastUsedAt);
    }

    private static string Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLower();
    }
}
