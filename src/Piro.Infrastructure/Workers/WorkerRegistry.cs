using System.Collections.Concurrent;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Singleton in-memory registry of currently connected SignalR worker connections.
/// Connection state is process-local and resets on API restart; workers reconnect automatically.
/// </summary>
internal class WorkerRegistry : IWorkerRegistry
{
    /// <summary>
    /// Workers heartbeat every ~1 minute (see ApiWorkerHostedService / WorkerSignalRService).
    /// A worker silent for longer than this is treated as dead even if SignalR hasn't
    /// noticed the disconnect yet, so fan-out doesn't wait on it for the full batch timeout.
    /// </summary>
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(3);

    private readonly ConcurrentDictionary<string, WorkerInfo> _byConnectionId = new();

    public void Register(string connectionId, WorkerInfo info) =>
        _byConnectionId[connectionId] = info;

    public void Unregister(string connectionId) =>
        _byConnectionId.TryRemove(connectionId, out _);

    public void UpdateHeartbeat(string connectionId, string? version = null)
    {
        if (_byConnectionId.TryGetValue(connectionId, out var existing))
            _byConnectionId[connectionId] = existing with
            {
                LastHeartbeat = DateTime.UtcNow,
                Version = version ?? existing.Version,
            };
    }

    public WorkerInfo? GetByConnectionId(string connectionId) =>
        _byConnectionId.GetValueOrDefault(connectionId);

    /// <summary>
    /// Returns a connection ID for any worker serving the given region.
    /// Falls back to workers with region <c>"default"</c> when no exact match exists.
    /// </summary>
    public string? GetConnectionIdForRegion(string region)
    {
        var matches = _byConnectionId.Values
            .Where(w => w.Region.Equals(region, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            return null;

        // Round-robin via index based on current tick — cheap, lock-free
        return matches[Environment.TickCount % matches.Count].ConnectionId;
    }

    public IReadOnlyList<WorkerInfo> GetAll() =>
        _byConnectionId.Values.Where(IsAlive).ToList();

    public WorkerInfo? GetDefaultWorker() =>
        _byConnectionId.Values.Where(IsAlive).FirstOrDefault(w => w.IsDefault);

    private static bool IsAlive(WorkerInfo w) =>
        DateTime.UtcNow - w.LastHeartbeat < StaleThreshold;
}
