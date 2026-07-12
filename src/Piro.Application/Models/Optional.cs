using System.Text.Json;
using System.Text.Json.Serialization;

namespace Piro.Application.Models;

/// <summary>
/// Distinguishes "field present in the JSON payload" from "field omitted" for PATCH-style
/// partial updates — something a plain nullable can't do, since <c>null</c> is the only way
/// to represent "empty" and is ambiguous between "clear this" and "don't touch this".
/// Use on optional-FK-clearing fields in Update*Request DTOs: <c>IsSet</c> is true only when
/// the client actually included the property (with any value, including null); <c>Value</c>
/// holds what they sent.
/// </summary>
[JsonConverter(typeof(OptionalJsonConverterFactory))]
public readonly struct Optional<T>
{
    public bool IsSet { get; }
    public T? Value { get; }

    private Optional(bool isSet, T? value)
    {
        IsSet = isSet;
        Value = value;
    }

    public static Optional<T> Unset => new(false, default);
    public static Optional<T> Of(T? value) => new(true, value);
}

public class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var innerType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionalJsonConverter<>).MakeGenericType(innerType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// Deserializes present-in-payload state via <see cref="Optional{T}.Of"/> — this converter is
/// only ever invoked by System.Text.Json when the property actually appears in the JSON, so
/// reaching <see cref="Read"/> at all already proves <c>IsSet</c> should be true.
/// </summary>
public class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
{
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<T>(ref reader, options);
        return Optional<T>.Of(value);
    }

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.Value, options);
}
