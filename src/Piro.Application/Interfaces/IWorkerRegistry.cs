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
}

/// <summary>Snapshot of a connected worker's state.</summary>
public record WorkerInfo(
    Guid WorkerId,
    string ConnectionId,
    string Region,
    DateTime ConnectedAt,
    DateTime LastHeartbeat,
    string? Version = null);
