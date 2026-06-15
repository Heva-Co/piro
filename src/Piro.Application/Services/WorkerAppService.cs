using System.Security.Cryptography;
using System.Text;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Application.Services;

/// <summary>Manages worker registration lifecycle: token generation, listing, and deletion.</summary>
public class WorkerAppService(
    IWorkerRegistrationRepository workerRepo,
    IWorkerRegistry registry)
{
    /// <summary>
    /// Registers a new worker, generates a long-lived worker token, and returns it once.
    /// The plaintext token is never stored — only its SHA-256 hash is persisted.
    /// </summary>
    public async Task<CreateWorkerResponse> CreateAsync(CreateWorkerRequest request, CancellationToken ct = default)
    {
        var workerToken = GenerateToken();
        var tokenHash = HashToken(workerToken);

        var registration = new WorkerRegistration
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Region = request.Region,
            WorkerTokenHash = tokenHash,
            IsActive = true
        };

        await workerRepo.CreateAsync(registration, ct);

        return new CreateWorkerResponse(
            registration.Id,
            registration.Name,
            registration.Region,
            workerToken,
            registration.CreatedAt);
    }


    /// <summary>Returns all registered workers enriched with live connection state.</summary>
    public async Task<IEnumerable<WorkerDto>> GetAllAsync(CancellationToken ct = default)
    {
        var registrations = await workerRepo.GetAllAsync(ct);
        var connected = registry.GetAll();

        return registrations.Select(r =>
        {
            var live = connected.FirstOrDefault(w => w.WorkerId == r.Id);
            var lastHeartbeat = live?.LastHeartbeat ?? r.LastHeartbeat;
            // Online only if the worker has an active in-memory connection.
            // OnDisconnectedAsync always fires (graceful or not), so the registry is authoritative.
            var isOnline = live != null;
            var isBuiltIn = r.WorkerTokenHash == "builtin";
            return new WorkerDto(
                r.Id,
                r.Name,
                r.Region,
                IsConnected: isOnline,
                LastHeartbeat: lastHeartbeat,
                r.CreatedAt,
                r.IsActive,
                Version: live?.Version,
                IsBuiltIn: isBuiltIn);
        });
    }

    /// <summary>Deactivates a worker registration and revokes its token.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var registration = await workerRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Worker {id} not found.");

        if (registration.WorkerTokenHash == "builtin")
            throw new InvalidOperationException("The built-in API worker cannot be deleted.");

        await workerRepo.DeleteAsync(registration, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
