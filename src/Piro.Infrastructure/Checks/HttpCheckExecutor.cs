using System.Diagnostics;
using System.Text.Json;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.TypeData;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Checks;

/// <summary>Executes an HTTP/HTTPS availability check.</summary>
internal class HttpCheckExecutor(IHttpClientFactory httpClientFactory) : ICheckExecutor
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CheckType CheckType => CheckType.HTTP;

    public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize<HttpCheckData>(check.TypeDataJson, _json)
                   ?? new HttpCheckData();

        if (string.IsNullOrWhiteSpace(data.Url))
            return new CheckExecutionResult(ServiceStatus.DOWN, null, "URL is not configured.");

        var client = httpClientFactory.CreateClient(data.FollowRedirects ? "piro-http" : "piro-http-noredirect");
        client.Timeout = TimeSpan.FromMilliseconds(data.TimeoutMs);

        using var request = new HttpRequestMessage(new HttpMethod(data.Method), data.Url);
        if (data.Headers is not null)
            foreach (var (key, value) in data.Headers)
                request.Headers.TryAddWithoutValidation(key, value);

        if (data.Body is not null)
            request.Content = new StringContent(data.Body);

        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();

            var isCodeOk = data.ExpectedStatusCodes is { Count: > 0 }
                ? data.ExpectedStatusCodes.Contains((int)response.StatusCode)
                : response.IsSuccessStatusCode;

            if (!isCodeOk)
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                    $"Unexpected status code: {(int)response.StatusCode}");

            if (data.ExpectedBodyContains is not null)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!body.Contains(data.ExpectedBodyContains, StringComparison.Ordinal))
                    return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                        $"Response body does not contain expected string.");
            }

            return new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, null);
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
}
