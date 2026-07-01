using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Piro.Application.Attributes;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.TypeData;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Integrations.GoogleCloud;

namespace Piro.Infrastructure.Checks;

[RequiresIntegration(IntegrationType.GoogleCloud)]
internal class GcpCloudRunJobCheckExecutor(
    IHttpClientFactory httpClientFactory,
    IGcpTokenProvider tokenProvider) : ICheckExecutor
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CheckType CheckType => CheckType.GCP_CloudRunJob;

    public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
    {
        try
        {
        return await ExecuteInternalAsync(check, ct);
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, $"Executor error: {ex.Message}");
        }
    }

    private async Task<CheckExecutionResult> ExecuteInternalAsync(Check check, CancellationToken ct)
    {
        var data = JsonSerializer.Deserialize<GcpCloudRunJobCheckData>(check.TypeDataJson, _json)
                   ?? new GcpCloudRunJobCheckData();

        if (string.IsNullOrWhiteSpace(data.ProjectId) || string.IsNullOrWhiteSpace(data.Region) || string.IsNullOrWhiteSpace(data.JobName))
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, "ProjectId, Region and JobName are required.");

        if (check.IntegrationId is null || check.Integration is null)
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, "A Google Cloud integration is required for this check.");

        string accessToken;
        try
        {
            accessToken = await tokenProvider.GetAccessTokenAsync(check.IntegrationId.Value, check.Integration.ConfigJson, ct);
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, $"Failed to obtain GCP access token: {ex.Message}");
        }

        var url = $"https://run.googleapis.com/v2/projects/{data.ProjectId}/locations/{data.Region}/jobs/{data.JobName}/executions";
        var client = httpClientFactory.CreateClient("piro-http");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(url, ct);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                    $"Cloud Run API returned {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ExecutionsListResponse>(json, _json);
            var executions = result?.Executions;

            if (executions is null || executions.Count == 0)
                return new CheckExecutionResult(ServiceStatus.NO_DATA, sw.Elapsed.TotalMilliseconds, "No executions found.");

            var latest = executions[0];
            var completedCondition = latest.Conditions?.FirstOrDefault(c =>
                string.Equals(c.Type, "Completed", StringComparison.OrdinalIgnoreCase));

            if (completedCondition is null || !string.Equals(completedCondition.Status, "True", StringComparison.OrdinalIgnoreCase))
                return new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, "Latest execution is still running.");

            if (latest.CreateTime.HasValue)
            {
                var age = DateTime.UtcNow - latest.CreateTime.Value;
                if (age.TotalHours > data.MaxAgeHours)
                    return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                        $"Last execution was {age.TotalHours:F1}h ago (threshold: {data.MaxAgeHours}h).");
            }

            var failed = latest.FailedCount ?? 0;
            var succeeded = latest.SucceededCount ?? 0;

            if (failed == 0)
                return new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, null);

            if (succeeded > 0)
                return new CheckExecutionResult(ServiceStatus.DEGRADED, sw.Elapsed.TotalMilliseconds,
                    $"{failed} task(s) failed, {succeeded} succeeded.");

            return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                $"All {failed} task(s) failed.");
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, "Request timed out.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    private record ExecutionsListResponse(
        [property: JsonPropertyName("executions")] List<ExecutionResource>? Executions);

    private record ExecutionResource(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("createTime")] DateTime? CreateTime,
        [property: JsonPropertyName("conditions")] List<ExecutionCondition>? Conditions,
        [property: JsonPropertyName("succeededCount")] int? SucceededCount,
        [property: JsonPropertyName("failedCount")] int? FailedCount);

    private record ExecutionCondition(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("status")] string? Status);
}
