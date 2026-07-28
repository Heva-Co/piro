using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Piro.Api.OpenApi;

/// <summary>
/// Replaces every reference to a <c>Patch&lt;T&gt;</c> component with the nullable schema of
/// <c>T</c>, then removes the now-unused component.
/// </summary>
/// <remarks>
/// <para>
/// <c>Patch&lt;T&gt;</c> exists only to distinguish "field omitted" from "field set to null"
/// (RFC 0019 §4.4). Its JSON converter unwraps it, so on the wire a client still sends a plain
/// <c>3</c> or <c>null</c> — the distinction comes from whether the property appears at all. The
/// schema has to say the same thing, or the generated TypeScript client describes a wrapper object
/// that no request ever contains.
/// </para>
/// <para>
/// A schema transformer cannot do this. It runs for the wrapper type, but the referencing property
/// emits a <c>$ref</c> to a named component, so editing that component in place leaves an empty
/// schema behind the reference and the client generates <c>unknown</c>. Substituting the reference
/// at the document level is what actually removes the wrapper.
/// </para>
/// </remarks>
internal sealed class PatchSchemaTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Matches the component names OpenAPI derives from <c>Patch&lt;T&gt;</c>, e.g. "PatchOfint".</summary>
    private const string ComponentPrefix = "PatchOf";

    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemas = document.Components?.Schemas;
        if (schemas is null) return Task.CompletedTask;

        var wrappers = schemas.Keys
            .Where(name => name.StartsWith(ComponentPrefix, StringComparison.Ordinal))
            .ToList();
        if (wrappers.Count == 0) return Task.CompletedTask;

        foreach (var schema in schemas.Values)
            Substitute(schema, wrappers);

        foreach (var wrapper in wrappers)
            schemas.Remove(wrapper);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Rewrites a property typed <c>oneOf: [null, $ref PatchOfX]</c> — the shape a nullable
    /// <c>Patch&lt;T&gt;?</c> produces — into the plain nullable scalar the wire actually carries.
    /// </summary>
    private static void Substitute(IOpenApiSchema schema, List<string> wrappers)
    {
        if (schema is not OpenApiSchema concrete || concrete.Properties is null) return;

        foreach (var (name, property) in concrete.Properties.ToList())
            if (WrapperName(property, wrappers) is { } wrapper)
                concrete.Properties[name] = Unwrapped(wrapper, property);
    }

    private static string? WrapperName(IOpenApiSchema property, List<string> wrappers)
    {
        if (property is OpenApiSchemaReference reference && wrappers.Contains(reference.Reference.Id ?? ""))
            return reference.Reference.Id;

        if (property is OpenApiSchema { OneOf: { } oneOf })
            foreach (var option in oneOf)
                if (option is OpenApiSchemaReference r && wrappers.Contains(r.Reference.Id ?? ""))
                    return r.Reference.Id;

        return null;
    }

    /// <summary>
    /// Builds the nullable schema for the wrapped type, read from the component name OpenAPI derived
    /// from the generic argument ("PatchOfint" → integer). An unrecognised inner type is left as an
    /// untyped nullable rather than guessed at, so a future <c>Patch&lt;T&gt;</c> over something else
    /// degrades to "anything" instead of being confidently mis-described.
    /// </summary>
    private static OpenApiSchema Unwrapped(string wrapperName, IOpenApiSchema original)
    {
        var inner = wrapperName[ComponentPrefix.Length..];

        var (type, format) = inner switch
        {
            "int" or "int32" => (JsonSchemaType.Integer, "int32"),
            "long" or "int64" => (JsonSchemaType.Integer, "int64"),
            "string" => (JsonSchemaType.String, null),
            "bool" or "boolean" => (JsonSchemaType.Boolean, null),
            "double" => (JsonSchemaType.Number, "double"),
            "Guid" => (JsonSchemaType.String, "uuid"),
            _ => (default, null),
        };

        return new OpenApiSchema
        {
            Type = type == default ? JsonSchemaType.Null : type | JsonSchemaType.Null,
            Format = format,
            Description = original.Description,
        };
    }
}
