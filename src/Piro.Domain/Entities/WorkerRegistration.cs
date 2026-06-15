namespace Piro.Domain.Entities;

/// <summary>
/// Represents a registered remote check worker.
/// The plaintext <c>workerToken</c> is returned once at creation time and never stored;
/// only its SHA-256 hex hash is persisted here.
/// </summary>
public class WorkerRegistration
{
    public Guid Id { get; set; }

    /// <summary>Human-readable label for this worker, e.g. "eu-west-1".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Region label forwarded to check result ingestion.</summary>
    public string Region { get; set; } = "default";

    /// <summary>SHA-256 hex hash of the long-lived worker token. Validated on every SignalR connection.</summary>
    public string WorkerTokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Updated on each heartbeat received from the worker (in-memory only for liveness; persisted here for persistence).</summary>
    public DateTime? LastHeartbeat { get; set; }

    public bool IsActive { get; set; } = true;
}
