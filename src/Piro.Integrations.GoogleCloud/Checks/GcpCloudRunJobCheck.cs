using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Piro.Checks.Abstractions;
using Piro.Contracts;

namespace Piro.Integrations.GoogleCloud;

/// <summary>
/// Verifies a Cloud Run Job completed within a freshness window. Ships with the GoogleCloud integration
/// (RFC 0016): it lives in this assembly and is only available when that integration is registered, and
/// it authenticates through the integration's own <see cref="IGcpTokenProvider"/> (resolved via the
/// check host) using the instance id in its config. Reports two simultaneous dimensions — last-run age
/// (hours) and failed-task count — leaving the severity verdict (e.g. partial failure → DEGRADED) to the
/// alert policy. Down only when the freshness window is breached or every task failed.
/// </summary>
public sealed class GcpCloudRunJobCheck : Check<GcpCloudRunJobCheckConfig>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public override string CheckId => "GCP_CloudRunJob";

    /// <summary>Hours since the job's last execution — older is worse.</summary>
    private static readonly DimensionSpec LastRunAge = new("LastRunAge", DimensionComparison.Threshold, ThresholdDirection.HigherIsWorse, "hours");

    /// <summary>Number of failed tasks in the latest execution — more is worse.</summary>
    private static readonly DimensionSpec FailedTasks = new("FailedTasks", DimensionComparison.Threshold, ThresholdDirection.HigherIsWorse, "count");

    public override CheckManifest Manifest => new()
    {
        Label = "GCP Cloud Run Job",
        Description = "Verify a Cloud Run Job has completed within a freshness window.",
        ConfigType = typeof(GcpCloudRunJobCheckConfig),
        Dimensions = [CommonDimensions.Status, LastRunAge, FailedTasks],
        RequiredIntegration = "GoogleCloud",
    };

    public override async Task<CheckProbeResult> ProbeAsync(GcpCloudRunJobCheckConfig config, ICheckHost host, CancellationToken ct = default)
    {
        try
        {
            return await ProbeInternalAsync(config, host, ct);
        }
        catch (TaskCanceledException)
        {
            return CheckProbeResult.DownWith("Request timed out.");
        }
        catch (Exception ex)
        {
            return CheckProbeResult.Failed($"Executor error: {ex.Message}");
        }
    }

    private static async Task<CheckProbeResult> ProbeInternalAsync(GcpCloudRunJobCheckConfig config, ICheckHost host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.ProjectId) || string.IsNullOrWhiteSpace(config.Region) || string.IsNullOrWhiteSpace(config.JobName))
            return CheckProbeResult.Failed("ProjectId, Region and JobName are required.");
        if (config.IntegrationInstanceId == Guid.Empty)
            return CheckProbeResult.Failed("A Google Cloud integration is required for this check.");

        string accessToken;
        try
        {
            accessToken = await host.GetRequiredService<IGcpTokenProvider>()
                .GetAccessTokenAsync(config.IntegrationInstanceId, ct);
        }
        catch (Exception ex)
        {
            return CheckProbeResult.Failed($"Failed to obtain GCP access token: {ex.Message}");
        }

        var jobUrl = $"https://run.googleapis.com/v2/projects/{config.ProjectId}/locations/{config.Region}/jobs/{config.JobName}";
        var client = host.GetRequiredService<IHttpClientFactory>().CreateClient("piro-http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var jobResponse = await client.GetAsync(jobUrl, ct);
        if (jobResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            return CheckProbeResult.Failed(
                $"Cloud Run job '{config.JobName}' does not exist in project '{config.ProjectId}', region '{config.Region}'.");

        using var response = await client.GetAsync($"{jobUrl}/executions", ct);
        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            return CheckProbeResult.DownWith(
                $"Cloud Run API returned {(int)response.StatusCode}: {errBody[..Math.Min(200, errBody.Length)]}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var executions = JsonSerializer.Deserialize<ExecutionsListResponse>(json, Json)?.Executions;

        // No execution ever, or none within the freshness window, is a Down: the job hasn't run when it
        // should have (per the config's Max age). Freshness is the check's job; the policy still owns
        // whether "age past threshold" is DOWN vs DEGRADED via the LastRunAge dimension.
        if (executions is null || executions.Count == 0)
            return CheckProbeResult.DownWith("No executions found for this job.");

        var latest = executions.OrderByDescending(e => e.CreateTime ?? DateTime.MinValue).First();
        var ageHours = latest.CreateTime.HasValue ? (DateTime.UtcNow - latest.CreateTime.Value).TotalHours : (double?)null;
        var ageDim = LastRunAge.Measure(ageHours ?? double.MaxValue);

        if (latest.CompletionTime is null)
            return CheckProbeResult.Ok(ageDim, Failed(0)) with { Message = "Latest execution is still running." };

        if (ageHours is { } age && age > config.MaxAgeHours)
            return CheckProbeResult.DownWith(
                $"Last execution was {age:F1}h ago (threshold: {config.MaxAgeHours}h).", ageDim, Failed(latest.FailedCount ?? 0));

        var failed = latest.FailedCount ?? 0;
        var succeeded = latest.SucceededCount ?? 0;

        // Every task failed = a real Down. A partial failure stays Up with FailedTasks>0 so the policy
        // can raise it to DEGRADED. A clean run is Up with FailedTasks=0.
        if (failed > 0 && succeeded == 0)
            return CheckProbeResult.DownWith($"All {failed} task(s) failed.", ageDim, Failed(failed));

        return CheckProbeResult.Ok(ageDim, Failed(failed));
    }

    private static CheckDimension Failed(int count) => FailedTasks.Measure(count);

    private record ExecutionsListResponse(
        [property: JsonPropertyName("executions")] List<ExecutionResource>? Executions);

    private record ExecutionResource(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("createTime")] DateTime? CreateTime,
        [property: JsonPropertyName("completionTime")] DateTime? CompletionTime,
        [property: JsonPropertyName("succeededCount")] int? SucceededCount,
        [property: JsonPropertyName("failedCount")] int? FailedCount);
}
