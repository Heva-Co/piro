using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Auth;

/// <summary>Creates, lists, and validates API keys.</summary>
/// <remarks>
/// Raw keys are never stored. Only the HMAC-SHA256 hash is persisted.
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
            Status = "ACTIVE"
        };

        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);

        return new ApiKeyCreatedResponse(entity.Id, entity.Name, rawKey, masked, entity.CreatedAt);
    }

    public async Task<IEnumerable<ApiKeyDto>> GetByUserAsync(int userId, CancellationToken ct = default)
    {
        var keys = await db.ApiKeys
            .Where(k => k.UserId == userId && k.Status == "ACTIVE")
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return keys.Select(k => new ApiKeyDto(k.Id, k.Name, k.MaskedKey, k.Status, k.CreatedAt));
    }

    public async Task RevokeAsync(int keyId, int userId, CancellationToken ct = default)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct)
            ?? throw new InvalidOperationException("API key not found.");

        key.Status = "REVOKED";
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Validates a raw API key and returns the owning user ID, or null if invalid.</summary>
    public async Task<int?> ValidateAsync(string rawKey, CancellationToken ct = default)
    {
        var hashed = Hash(rawKey);
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.HashedKey == hashed && k.Status == "ACTIVE", ct);
        return key?.UserId;
    }

    private static string Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLower();
    }
}
