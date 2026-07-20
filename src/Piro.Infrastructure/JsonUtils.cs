using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Piro.Infrastructure;

internal static class JsonUtils
{
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, CaseInsensitive);

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, CaseInsensitive);

    /// <summary>
    /// Deserializes <paramref name="json"/> and validates all <see cref="RequiredAttribute"/> constraints.
    /// Throws <see cref="InvalidOperationException"/> if deserialization fails or any required field is missing/empty.
    /// </summary>
    public static T DeserializeAndValidate<T>(string json) where T : class
    {
        var obj = JsonSerializer.Deserialize<T>(json, CaseInsensitive)
            ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(obj, new ValidationContext(obj), results, validateAllProperties: true))
            throw new InvalidOperationException(
                $"Invalid {typeof(T).Name}: {string.Join("; ", results.Select(r => r.ErrorMessage))}");

        return obj;
    }
}
