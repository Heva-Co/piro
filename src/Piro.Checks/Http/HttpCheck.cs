using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using Json.Path;
using Piro.Checks.Abstractions;
using Piro.Contracts;

namespace Piro.Checks;

/// <summary>
/// Probes an HTTP/HTTPS endpoint. Down on a connection failure, timeout, unexpected status code, or a
/// failed hard body-rule. A failed <em>soft</em> body-rule keeps the probe Up but is counted in the
/// "BodyRuleFailures" dimension so the alert policy can raise severity (e.g. to DEGRADED). Latency is
/// always reported. The check never decides severity itself.
/// </summary>
public sealed class HttpCheck : Check<HttpCheckConfig>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public override string CheckId => "HTTP";

    /// <summary>Count of failed soft (non-fatal) body rules on the last probe — more is worse.</summary>
    private static readonly DimensionSpec BodyRuleFailuresSpec = new("BodyRuleFailures", DimensionComparison.Threshold, ThresholdDirection.HigherIsWorse, "count");

    public override CheckManifest Manifest => new()
    {
        Label = "HTTP",
        Description = "Fetch a URL and assert on the status code and response body.",
        ConfigType = typeof(HttpCheckConfig),
        Dimensions = [CommonDimensions.Status, CommonDimensions.Latency, BodyRuleFailuresSpec],
    };

    public override async Task<CheckProbeResult> ProbeAsync(HttpCheckConfig config, ICheckHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
            return CheckProbeResult.Failed("URL is not configured.");

        // Redirect policy is a per-check config, so pick the matching named client from the factory
        // rather than a single shared HttpClient (the host has no knowledge of this check's config).
        var client = host.GetRequiredService<IHttpClientFactory>()
            .CreateClient(config.FollowRedirects ? "piro-http" : "piro-http-noredirect");
        client.Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs);

        using var request = new HttpRequestMessage(new HttpMethod(config.Method), config.Url);
        if (config.Headers is not null)
            foreach (var (key, value) in config.Headers)
                request.Headers.TryAddWithoutValidation(key, value);
        if (config.Body is not null)
            request.Content = new StringContent(config.Body);

        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();
            var latency = Latency(sw);

            var statusCode = (int)response.StatusCode;
            var isCodeOk = config.ExpectedStatusCodes is { Count: > 0 }
                ? config.ExpectedStatusCodes.Any(e => MatchesStatusCode(e, statusCode))
                : response.IsSuccessStatusCode;
            if (!isCodeOk)
                return CheckProbeResult.DownWith($"Unexpected status code: {statusCode}", latency);

            if (config.ResponseRules is { Count: > 0 })
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var softFailures = 0;
                string? firstSoftMessage = null;
                foreach (var rule in config.ResponseRules)
                {
                    var (passed, error) = EvaluateRule(rule, body);
                    if (passed) continue;
                    // A hard rule fails the probe outright; a soft rule keeps it Up but is counted so the
                    // policy can decide severity (the old rule.Degraded flag becomes a policy concern).
                    if (!rule.Degraded)
                        return CheckProbeResult.DownWith(error ?? "Response rule failed.", latency);
                    softFailures++;
                    firstSoftMessage ??= error;
                }

                if (softFailures > 0)
                    return CheckProbeResult.Ok(latency, BodyRuleFailures(softFailures)) with { Message = firstSoftMessage };
            }

            return CheckProbeResult.Ok(latency, BodyRuleFailures(0));
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return CheckProbeResult.DownWith("Request timed out.", Latency(sw));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return CheckProbeResult.DownWith(ex.Message, Latency(sw));
        }
    }

    private static CheckDimension Latency(Stopwatch sw) =>
        CommonDimensions.Latency.Measure(sw.Elapsed.TotalMilliseconds);

    private static CheckDimension BodyRuleFailures(int count) =>
        BodyRuleFailuresSpec.Measure(count);

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
                    ? (true, null) : (false, $"Response body does not contain: {rule.Value}"),
                "not_contains" => !body.Contains(rule.Value, StringComparison.Ordinal)
                    ? (true, null) : (false, $"Response body unexpectedly contains: {rule.Value}"),
                "regex" => Regex.IsMatch(body, rule.Value)
                    ? (true, null) : (false, $"Response body does not match regex: {rule.Value}"),
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
        if (node is null) return (false, "Response body is not valid JSON.");
        var result = JsonPath.Parse(rule.Value).Evaluate(node);
        if (result.Matches is not { Count: > 0 })
            return (false, $"JSONPath '{rule.Value}' returned no matches.");
        if (rule.Expected is null) return (true, null);
        var actual = result.Matches[0].Value?.ToString();
        return actual == rule.Expected
            ? (true, null) : (false, $"JSONPath '{rule.Value}' = '{actual}', expected '{rule.Expected}'.");
    }

    private static (bool passed, string? error) EvaluateXPath(HttpResponseRule rule, string body)
    {
        var doc = new XmlDocument();
        doc.LoadXml(body);
        var result = doc.CreateNavigator()!.SelectSingleNode(rule.Value);
        if (result is null) return (false, $"XPath '{rule.Value}' returned no match.");
        if (rule.Expected is null) return (true, null);
        return result.Value == rule.Expected
            ? (true, null) : (false, $"XPath '{rule.Value}' = '{result.Value}', expected '{rule.Expected}'.");
    }
}
