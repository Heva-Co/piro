using System.Collections.Concurrent;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Singleton in-memory registry of currently connected SignalR worker connections.
/// Connection state is process-local and resets on API restart; workers reconnect automatically.
/// </summary>
internal class WorkerRegistry : IWorkerRegistry
{
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
        _byConnectionId.Values.ToList();

    public WorkerInfo? GetDefaultWorker() =>
        _byConnectionId.Values.FirstOrDefault(w => w.IsDefault);
}
