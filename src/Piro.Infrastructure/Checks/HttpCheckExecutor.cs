using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using Json.Path;
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
        try
        {
            var data = JsonSerializer.Deserialize<HttpCheckData>(check.TypeDataJson, _json)
                       ?? new HttpCheckData();

            if (string.IsNullOrWhiteSpace(data.Url))
                return new CheckExecutionResult(ServiceStatus.FAILURE, null, "URL is not configured.");

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

                // 1. Status code check
                var statusCode = (int)response.StatusCode;
                var isCodeOk = data.ExpectedStatusCodes is { Count: > 0 }
                    ? data.ExpectedStatusCodes.Any(e => MatchesStatusCode(e, statusCode))
                    : response.IsSuccessStatusCode;

                if (!isCodeOk)
                    return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                        $"Unexpected status code: {statusCode}");

                // 2. Response body rules
                if (data.ResponseRules is { Count: > 0 })
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    foreach (var rule in data.ResponseRules)
                    {
                        var (passed, error) = EvaluateRule(rule, body);
                        if (!passed)
                        {
                            var status = rule.Degraded ? ServiceStatus.DEGRADED : ServiceStatus.DOWN;
                            return new CheckExecutionResult(status, sw.Elapsed.TotalMilliseconds, error);
                        }
                    }
                }

                // 3. Latency thresholds (checked after content rules)
                var latencyMs = sw.Elapsed.TotalMilliseconds;
                if (data.DownLatencyMs.HasValue && latencyMs >= data.DownLatencyMs.Value)
                    return new CheckExecutionResult(ServiceStatus.DOWN, latencyMs,
                        $"Response time {latencyMs:F0}ms exceeded down threshold ({data.DownLatencyMs}ms).");

                if (data.DegradedLatencyMs.HasValue && latencyMs >= data.DegradedLatencyMs.Value)
                    return new CheckExecutionResult(ServiceStatus.DEGRADED, latencyMs,
                        $"Response time {latencyMs:F0}ms exceeded degraded threshold ({data.DegradedLatencyMs}ms).");

                return new CheckExecutionResult(ServiceStatus.UP, latencyMs, null);
            }
            catch (TaskCanceledException)
            {
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, "Request timed out.");
            }
            catch (Exception ex)
            {
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, ex.Message);
            }
            finally
            {
                sw.Stop();
            }
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, $"Executor error: {ex.Message}");
        }
    }

    private static bool MatchesStatusCode(string pattern, int actual)
    {
        if (pattern.Length == 3 && pattern[1] == 'x' && pattern[2] == 'x' &&
            int.TryParse(pattern[0].ToString(), out var hundreds))
            return actual / 100 == hundreds;

        return int.TryParse(pattern, out var exact) && exact == actual;
    }

    private static (bool passed, string? error) EvaluateRule(HttpResponseRule rule, string body)
    {
        try
        {
            return rule.Type switch
            {
                "contains" => body.Contains(rule.Value, StringComparison.Ordinal)
                    ? (true, null)
                    : (false, $"Response body does not contain: {rule.Value}"),

                "not_contains" => !body.Contains(rule.Value, StringComparison.Ordinal)
                    ? (true, null)
                    : (false, $"Response body unexpectedly contains: {rule.Value}"),

                "regex" => Regex.IsMatch(body, rule.Value)
                    ? (true, null)
                    : (false, $"Response body does not match regex: {rule.Value}"),

                "json_path" => EvaluateJsonPath(rule, body),

                "xml_path" => EvaluateXPath(rule, body),

                _ => (false, $"Unknown rule type: {rule.Type}")
            };
        }
        catch (Exception ex)
        {
            return (false, $"Rule evaluation error ({rule.Type}): {ex.Message}");
        }
    }

    private static (bool passed, string? error) EvaluateJsonPath(HttpResponseRule rule, string body)
    {
        var node = JsonNode.Parse(body);
        if (node is null)
            return (false, "Response body is not valid JSON.");

        var path = JsonPath.Parse(rule.Value);
        var result = path.Evaluate(node);

        if (result.Matches is not { Count: > 0 })
            return (false, $"JSONPath '{rule.Value}' returned no matches.");

        if (rule.Expected is null)
            return (true, null);

        var actual = result.Matches[0].Value?.ToString();
        return actual == rule.Expected
            ? (true, null)
            : (false, $"JSONPath '{rule.Value}' = '{actual}', expected '{rule.Expected}'.");
    }

    private static (bool passed, string? error) EvaluateXPath(HttpResponseRule rule, string body)
    {
        var doc = new XmlDocument();
        doc.LoadXml(body);
        var nav = doc.CreateNavigator()!;

        var result = nav.SelectSingleNode(rule.Value);
        if (result is null)
            return (false, $"XPath '{rule.Value}' returned no match.");

        if (rule.Expected is null)
            return (true, null);

        var actual = result.Value;
        return actual == rule.Expected
            ? (true, null)
            : (false, $"XPath '{rule.Value}' = '{actual}', expected '{rule.Expected}'.");
    }
}
