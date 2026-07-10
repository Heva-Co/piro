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
    public async Task<int?> ValidateAsync(string rawKey, CancellationToken ct = default)
    {
        var hashed = Hash(rawKey);
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.HashedKey == hashed && k.Status == ApiKeyStatus.Active, ct);
        if (key is null)
            return null;

        key.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return key.UserId;
    }

    private static string Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLower();
    }
}
