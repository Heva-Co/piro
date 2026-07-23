using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Piro.Domain.Attributes;
using Piro.Contracts;

namespace Piro.Domain.Checks.Config;

/// <summary>Configuration for an HTTP check.</summary>
public record HttpCheckConfig
{
    [ConfigField("URL", Placeholder = "https://example.com/health", HelpText = "The URL to request.")]
    [Required, Url]
    public string Url { get; init; } = string.Empty;

    [ConfigField("Method")]
    [ConfigFieldOptions("GET", "POST", "PUT", "PATCH", "DELETE", "HEAD")]
    public string Method { get; init; } = "GET";

    [ConfigField("Headers", HelpText = "Headers sent with the request.")]
    public Dictionary<string, string>? Headers { get; init; }

    [ConfigField("Body", HelpText = "Request body, for POST/PUT/PATCH.")]
    [MultilineField]
    [VisibleWhen("method", "POST", "PUT", "PATCH")]
    public string? Body { get; init; }

    /// <summary>Timeout in milliseconds. Accepts both "timeout" and "timeoutMs" from JSON.</summary>
    [ConfigField("Timeout (ms)", HelpText = "Abort the request after this many milliseconds. Must be shorter than the check interval.")]
    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 5000;

    [ConfigField("Follow redirects")]
    public bool FollowRedirects { get; init; } = true;

    /// <summary>
    /// Accepted HTTP status codes or classes ("2xx", "3xx", "200", "301", etc.).
    /// Accepts both string ("200") and legacy integer (200) JSON values.
    /// When null or empty, any 2xx is treated as UP.
    /// </summary>
    [ConfigField("Expected status codes", Placeholder = "2xx", HelpText = "Accepted codes or classes (e.g. 200, 2xx). Empty = any 2xx is UP.")]
    [ConfigValidation("statusCodes")]
    [JsonPropertyName("expectedStatusCodes")]
    [JsonConverter(typeof(StatusCodeListConverter))]
    public List<string>? ExpectedStatusCodes { get; init; }

    /// <summary>Response body rules evaluated in order; first failure wins.</summary>
    [ConfigField("Response rules", HelpText = "Assertions on the response body; evaluated in order, first failure wins.")]
    public List<HttpResponseRule>? ResponseRules { get; init; }
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
    [ConfigField("Type")]
    [ConfigFieldOptions("contains", "not_contains", "regex", "json_path", "xml_path")]
    public string Type { get; init; } = "contains";

    /// <summary>The pattern, substring, regex, JSONPath expression, or XPath expression.</summary>
    [ConfigField("Value", HelpText = "The substring, regex, JSONPath, or XPath expression.")]
    [Required]
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// For "json_path" and "xml_path": the expected string value at the resolved path.
    /// For "contains" / "not_contains" / "regex": ignored.
    /// </summary>
    [ConfigField("Expected", HelpText = "For json_path / xml_path: the expected value at the resolved path.")]
    public string? Expected { get; init; }

    /// <summary>When true, a failing rule marks the check as DEGRADED instead of DOWN.</summary>
    [ConfigField("Degraded", HelpText = "When set, a failure marks the check DEGRADED instead of DOWN.")]
    public bool Degraded { get; init; }
}
