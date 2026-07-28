using System.Text.Json;
using System.Text.Json.Serialization;

namespace Piro.Application.DTOs;

/// <summary>
/// A field that is present in a patch payload, wrapping the value it was set to. A property typed
/// <c>Patch&lt;T&gt;?</c> distinguishes three states that a bare nullable cannot: the property is
/// absent (null <c>Patch</c> — leave the stored value alone), present and null (<c>Patch</c> whose
/// <see cref="Value"/> is null — clear it), or present with a value (RFC 0019 §4.4).
/// </summary>
/// <remarks>
/// Only needed for fields that are themselves nullable on the entity. For a non-nullable field a
/// plain <c>T?</c> already carries the distinction, since null can only mean "omitted".
/// </remarks>
[JsonConverter(typeof(PatchJsonConverterFactory))]
public readonly record struct Patch<T>(T Value)
{
    public static implicit operator Patch<T>(T value) => new(value);
}

/// <summary>
/// Serializes <see cref="Patch{T}"/> transparently as its underlying value, so the wire format is
/// unchanged — a client still sends <c>"escalationPolicyId": 3</c> or <c>null</c>, and the
/// present-versus-absent distinction comes from whether the JSON property appears at all.
/// </summary>
internal sealed class PatchJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Patch<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(PatchJsonConverter<>).MakeGenericType(valueType))!;
    }
}

internal sealed class PatchJsonConverter<T> : JsonConverter<Patch<T>>
{
    public override Patch<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(JsonSerializer.Deserialize<T>(ref reader, options)!);

    public override void Write(Utf8JsonWriter writer, Patch<T> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.Value, options);

    /// <summary>
    /// A <c>Patch&lt;T&gt;</c> is never "null" on the wire — a JSON null deserializes into a present
    /// patch carrying a null value, which is exactly the explicit-clear case.
    /// </summary>
    public override bool HandleNull => true;
}
