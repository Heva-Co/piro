using System.Text.Json;
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
    /// Accepts both string ("200") and legacy integer (200) JSON values.
    /// When null or empty, any 2xx is treated as UP.
    /// </summary>
    [JsonPropertyName("expectedStatusCodes")]
    [JsonConverter(typeof(StatusCodeListConverter))]
    public List<string>? ExpectedStatusCodes { get; init; }

    /// <summary>Response body rules evaluated in order; first failure wins.</summary>
    public List<HttpResponseRule>? ResponseRules { get; init; }

    /// <summary>Latency threshold in milliseconds to trigger DEGRADED. Optional.</summary>
    public int? DegradedLatencyMs { get; init; }

    /// <summary>Latency threshold in milliseconds to trigger DOWN. Optional.</summary>
    public int? DownLatencyMs { get; init; }
}

/// <summary>Deserializes status codes accepting both string ("200", "2xx") and legacy integer (200) JSON values.</summary>
internal sealed class StatusCodeListConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartArray) return null;

        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
                list.Add(reader.GetString()!);
            else if (reader.TokenType == JsonTokenType.Number)
                list.Add(reader.GetInt32().ToString());
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        writer.WriteStartArray();
        foreach (var s in value) writer.WriteStringValue(s);
        writer.WriteEndArray();
    }
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
