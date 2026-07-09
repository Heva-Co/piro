using System.Text.Json.Serialization;

namespace Piro.Application.Models.TypeData;

/// <summary>Configuration for an HTTP check.</summary>
public record HttpCheckData
{
    public string Url { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public Dictionary<string, string>? Headers { get; init; }
    public string? Body { get; init; }

    /// <summary>Timeout in milliseconds. Accepts both "timeout" and "timeoutMs" from JSON.</summary>
    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 5000;

    public bool FollowRedirects { get; init; } = true;

    /// <summary>
    /// Accepted HTTP status codes or classes ("2xx", "3xx", "200", "301", etc.).
    /// When null or empty, any 2xx is treated as UP.
    /// </summary>
    [JsonPropertyName("expectedStatusCodes")]
    public List<string>? ExpectedStatusCodes { get; init; }

    /// <summary>Response body rules evaluated in order; first failure wins.</summary>
    public List<HttpResponseRule>? ResponseRules { get; init; }

    /// <summary>Latency threshold in milliseconds to trigger DEGRADED. Optional.</summary>
    public int? DegradedLatencyMs { get; init; }

    /// <summary>Latency threshold in milliseconds to trigger DOWN. Optional.</summary>
    public int? DownLatencyMs { get; init; }
}

/// <summary>A single response body assertion rule.</summary>
public record HttpResponseRule
{
    /// <summary>Rule type: "contains", "not_contains", "regex", "json_path", "xml_path".</summary>
    public string Type { get; init; } = "contains";

    /// <summary>The pattern, substring, regex, JSONPath expression, or XPath expression.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// For "json_path" and "xml_path": the expected string value at the resolved path.
    /// For "contains" / "not_contains" / "regex": ignored.
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>When true, a failing rule marks the check as DEGRADED instead of DOWN.</summary>
    public bool Degraded { get; init; }
}
