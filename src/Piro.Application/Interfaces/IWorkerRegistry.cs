namespace Piro.Application.Interfaces;

/// <summary>In-memory registry of currently connected SignalR worker connections.</summary>
public interface IWorkerRegistry
{
    void Register(string connectionId, WorkerInfo info);
    void Unregister(string connectionId);
    void UpdateHeartbeat(string connectionId, string? version = null);
    WorkerInfo? GetByConnectionId(string connectionId);

    /// <summary>Returns a connection ID for a worker serving the given region, or <c>null</c> if none is connected.</summary>
    string? GetConnectionIdForRegion(string region);

    IReadOnlyList<WorkerInfo> GetAll();

    /// <summary>Returns the connected worker marked as default, or <c>null</c> if none is connected.</summary>
    WorkerInfo? GetDefaultWorker();
}

/// <summary>Snapshot of a connected worker's state.</summary>
public record WorkerInfo(
    Guid WorkerId,
    string ConnectionId,
    string Region,
    DateTime ConnectedAt,
    DateTime LastHeartbeat,
    string? Version = null,
    bool IsDefault = false)
{
    /// <summary>
    /// True for the built-in API worker, which executes checks IN-PROCESS rather than over SignalR. Its
    /// <see cref="ConnectionId"/> is a synthetic sentinel, not a real hub connection, so the dispatcher
    /// must run it via the local executor instead of sending a hub message that would silently go nowhere.
    /// </summary>
    public bool IsInProcess { get; init; }

    /// <summary>
    /// The worker's advertised tags (key → value, value null for a key-only tag), loaded at connect time.
    /// Used by the Part B scheduler to match a check's required worker tags (RFC 0008 §4.5). Empty when the
    /// worker carries no tags.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Tags { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);
}
