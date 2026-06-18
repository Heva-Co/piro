namespace Piro.Application.DTOs;

/// <summary>API representation of a registered worker, including live connection state.</summary>
public record WorkerDto(
    Guid Id,
    string Name,
    string Region,
    bool IsConnected,
    DateTime? LastHeartbeat,
    DateTime CreatedAt,
    bool IsActive,
    string? Version = null,
    bool IsBuiltIn = false,
    bool IsDefault = false);

public record CreateWorkerRequest(string Name, string Region, bool IsDefault = false);

public record UpdateWorkerRequest(string? Region = null);

/// <summary>
/// Returned once at creation time. <see cref="WorkerToken"/> is the plaintext token
/// the worker process needs — it is never stored and cannot be retrieved again.
/// </summary>
public record CreateWorkerResponse(
    Guid Id,
    string Name,
    string Region,
    string WorkerToken,
    DateTime CreatedAt);
