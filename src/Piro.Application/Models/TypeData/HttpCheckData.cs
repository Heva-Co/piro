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

    /// <summary>Accepted HTTP status codes. When null or empty, any 2xx is treated as UP.</summary>
    [JsonPropertyName("expectedStatusCodes")]
    public List<int>? ExpectedStatusCodes { get; init; }

    /// <summary>Substring that must appear in the response body for the check to pass.</summary>
    public string? ExpectedBodyContains { get; init; }
}
