using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Piro.Infrastructure.Logging;

/// <summary>
/// Serilog sink that persists log events to the Piro database via EF Core.
/// Uses periodic batching to avoid blocking the logging pipeline.
/// </summary>
public sealed class EfCoreLogSink(IServiceScopeFactory scopeFactory) : IBatchedLogEventSink
{
    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PiroDbContext>();

        foreach (var e in batch)
        {
            var properties = e.Properties.Count > 0
                ? JsonSerializer.Serialize(e.Properties.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToString()))
                : null;

            e.Properties.TryGetValue("SourceContext", out var sc);

            db.PiroLogs.Add(new PiroLog
            {
                Timestamp = e.Timestamp.UtcDateTime,
                Level = e.Level.ToString(),
                Message = e.RenderMessage(),
                Exception = e.Exception?.ToString(),
                Properties = properties,
                SourceContext = sc?.ToString()?.Trim('"'),
            });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (
            ex.InnerException?.Message.Contains("relation") == true ||
            ex.InnerException?.Message.Contains("does not exist") == true ||
            ex.InnerException?.Message.Contains("42P01") == true)
        {
            // Table not yet created (first startup before migrations run) — discard silently.
        }
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;
}
