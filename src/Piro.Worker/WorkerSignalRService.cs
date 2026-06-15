using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.Worker;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Worker;

/// <summary>
/// Manages the SignalR connection to the Piro API hub, handles inbound Execute messages,
/// runs the appropriate check executor, and sends results back.
/// Also fires heartbeats every 30 seconds on the same connection.
/// </summary>
public class WorkerSignalRService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<WorkerSignalRService> logger) : BackgroundService
{
    private HubConnection? _connection;

    public HubConnection? Connection => _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var apiUrl = configuration["PIRO_API_URL"]
            ?? throw new InvalidOperationException("PIRO_API_URL environment variable is required.");
        var workerToken = configuration["PIRO_WORKER_TOKEN"]
            ?? throw new InvalidOperationException("PIRO_WORKER_TOKEN environment variable is required.");

        _connection = new HubConnectionBuilder()
            .WithUrl($"{apiUrl.TrimEnd('/')}/hub/worker",
                opts => opts.AccessTokenProvider = () => Task.FromResult<string?>(workerToken))
            .WithAutomaticReconnect([
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            ])
            .AddJsonProtocol(opts =>
                opts.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        // Resolved on successful auth (hub sends Ack).
        // Faulted immediately when Reconnecting fires before Ack — that means hub rejected the token.
        var firstAckTcs = new TaskCompletionSource<WorkerAckMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstAckReceived = false;

        var rawVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var version = rawVersion is not null ? $"v{rawVersion.Major}.{rawVersion.Minor}.{rawVersion.Build}" : "unknown";
        var heartbeatMessage = new WorkerHeartbeatMessage(version);

        _connection.On<WorkerAckMessage>("Ack", msg =>
        {
            firstAckReceived = true;
            firstAckTcs.TrySetResult(msg);
            logger.LogInformation("Connected to API hub. WorkerId={WorkerId} Region={Region}",
                msg.WorkerId, msg.Region);
        });

        _connection.On<WorkerExecuteMessage>("Execute", async msg =>
            await HandleExecuteAsync(msg, stoppingToken));

        _connection.Reconnecting += ex =>
        {
            if (!firstAckReceived)
            {
                // Reconnecting before the first Ack means the hub rejected the connection
                // (called Context.Abort()). Fail fast instead of retrying forever.
                firstAckTcs.TrySetException(new InvalidOperationException(
                    "API hub rejected the worker token — check PIRO_WORKER_TOKEN is correct and the worker registration is active."));
                return Task.CompletedTask;
            }
            // WebSocketException / IOException = normal disconnect (API restart, network blip) — no stack trace needed
            if (ex is System.Net.WebSockets.WebSocketException or IOException)
                logger.LogWarning("Connection lost — reconnecting. ({Reason})", ex?.Message);
            else
                logger.LogWarning(ex, "Connection lost — reconnecting.");
            return Task.CompletedTask;
        };
        _connection.Reconnected += async _ =>
        {
            logger.LogInformation("Reconnected to API (version={Version}).", version);
            try { await _connection.InvokeAsync("Heartbeat", heartbeatMessage, stoppingToken); }
            catch (Exception ex) { logger.LogWarning(ex, "Post-reconnect heartbeat failed."); }
        };
        _connection.Closed += ex =>
        {
            firstAckTcs.TrySetException(
                ex ?? new Exception("Connection closed before acknowledgement — check PIRO_WORKER_TOKEN."));
            return Task.CompletedTask;
        };

        await _connection.StartAsync(stoppingToken);

        // Wait for hub acknowledgement. If the token is invalid this throws immediately
        // (Reconnecting fires → TCS faulted) with a descriptive error.
        var ack = await firstAckTcs.Task.WaitAsync(TimeSpan.FromSeconds(30), stoppingToken);

        // Send an immediate heartbeat so the version is visible right after connect
        try { await _connection.InvokeAsync("Heartbeat", heartbeatMessage, stoppingToken); }
        catch (Exception ex) { logger.LogWarning(ex, "Initial heartbeat failed."); }

        // Heartbeat loop — keeps the connection alive and the WorkerRegistry up-to-date
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await heartbeatTimer.WaitForNextTickAsync(stoppingToken))
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                try { await _connection.InvokeAsync("Heartbeat", heartbeatMessage, stoppingToken); }
                catch (Exception ex) { logger.LogWarning(ex, "Heartbeat failed."); }
            }
        }
    }

    private async Task HandleExecuteAsync(WorkerExecuteMessage msg, CancellationToken ct)
    {
        CheckExecutionResult result;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var executors = scope.ServiceProvider
                .GetRequiredService<IEnumerable<ICheckExecutor>>()
                .ToDictionary(e => e.CheckType);

            if (!executors.TryGetValue(msg.CheckType, out var executor))
            {
                logger.LogWarning("No executor registered for check type {CheckType}. Skipping check {CheckId}.",
                    msg.CheckType, msg.CheckId);
                return;
            }

            // Reconstruct a minimal Check entity — executors only need Id, Type, and TypeDataJson
            var check = new Check
            {
                Id = msg.CheckId,
                Type = msg.CheckType,
                TypeDataJson = msg.TypeDataJson
            };

            logger.LogInformation("Executing check {CheckId} (type={CheckType}).", msg.CheckId, msg.CheckType);
            result = await executor.ExecuteAsync(check, ct);
            logger.LogInformation("Check {CheckId} done — status={Status} latency={LatencyMs}ms.",
                msg.CheckId, result.Status, result.LatencyMs?.ToString("F0") ?? "—");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error executing check {CheckId}.", msg.CheckId);
            result = new CheckExecutionResult(ServiceStatus.DOWN, null, ex.Message);
        }

        var response = new WorkerResultMessage(
            msg.JobId,
            msg.CheckId,
            result.Status.ToString(),
            result.LatencyMs,
            result.ErrorMessage,
            DateTime.UtcNow,
            msg.BatchId);

        try
        {
            await _connection!.InvokeAsync("Result", response, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send result for check {CheckId} back to API.", msg.CheckId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            await _connection.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
