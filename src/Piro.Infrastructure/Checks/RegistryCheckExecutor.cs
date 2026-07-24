using System.Text.Json;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Checks.Abstractions;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Checks;

/// <summary>
/// The single bridge between Piro's execution pipeline and the RFC 0016 check SDK. Resolves the
/// <see cref="ICheck"/> for a check's type discriminator from the registry, deserializes its stored
/// config into the check's declared config type, runs the probe through the allow-listed
/// <see cref="ICheckHost"/>, and maps the raw <see cref="CheckProbeResult"/> to a
/// <see cref="CheckExecutionResult"/>. Replaces the seven per-type executors — a new check is just a new
/// registry entry, never a new executor here.
/// </summary>
internal sealed class RegistryCheckExecutor(ICheckRegistry registry, ICheckHost host, CurrentCheckContext currentCheck) : ICheckExecutor
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
    {
        var checkImpl = registry.Find(check.Type.ToString());
        if (checkImpl is null)
            return CheckExecutionResult.Of(ServiceStatus.NO_DATA, $"No check is registered for type {check.Type}.");

        // A check that reads its own history (RFC 0013 — Heartbeat) declares ConsumesCheckPoints; bind the
        // scoped reader to THIS check so its IOwnCheckPoints returns only this check's points. Declared,
        // not name-matched — the boundary is untouched for every check that leaves the flag false.
        if (checkImpl.Manifest.ConsumesCheckPoints)
            currentCheck.CheckId = check.Id;

        object config;
        try
        {
            config = JsonSerializer.Deserialize(check.TypeDataJson ?? "{}", checkImpl.Manifest.ConfigType, Json)
                     ?? Activator.CreateInstance(checkImpl.Manifest.ConfigType)!;
        }
        catch (Exception ex)
        {
            return CheckExecutionResult.Of(ServiceStatus.FAILURE, $"Invalid check config: {ex.Message}");
        }

        CheckProbeResult probe;
        try
        {
            probe = await checkImpl.ProbeAsync(config, host, ct);
        }
        catch (Exception ex)
        {
            return CheckExecutionResult.Of(ServiceStatus.FAILURE, $"Executor error: {ex.Message}");
        }

        var dimensions = probe.Dimensions.Count == 0
            ? new Dictionary<string, double>()
            : probe.Dimensions.ToDictionary(d => d.Name, d => d.Value);

        return new CheckExecutionResult(MapStatus(probe.Outcome), dimensions, probe.Message);
    }

    /// <summary>
    /// Maps the check's raw outcome to a service status. The check never decides severity: an Up is UP, a
    /// Down is DOWN, and an Error (the check itself could not run) is FAILURE — which the ingester treats
    /// apart from a real outage. DEGRADED is never produced here; it is the alert policy's decision.
    /// </summary>
    private static ServiceStatus MapStatus(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.Up => ServiceStatus.UP,
        CheckOutcome.Down => ServiceStatus.DOWN,
        CheckOutcome.Error => ServiceStatus.FAILURE,
        _ => ServiceStatus.NO_DATA,
    };
}
