using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Checks.Abstractions;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.Application.Config;

/// <summary>
/// Generates the JSON Schema for <c>piro.yaml</c> from the live check registry (RFC 0019 §4.10).
/// </summary>
/// <remarks>
/// <para>
/// Generated, never hand-written, and generated from the same <see cref="ConfigSchemaBuilder"/>
/// reflection that drives the admin panel's dynamic check form. That is the whole point: a
/// hand-authored schema drifts the moment a check adds a field, whereas this one cannot disagree
/// with what the server actually deserializes.
/// </para>
/// <para>
/// <c>type_data</c> is the hard part, because its shape depends on the check's type. The schema
/// expresses that as a chain of conditionals — one <c>if type is X then type_data looks like Y</c>
/// per registered check — so an editor completes <c>url</c> inside an HTTP check and <c>hostname</c>
/// inside a DNS one, and flags a field that belongs to neither.
/// </para>
/// <para>
/// Because it is built from the registry of the instance serving it, an instance with check types
/// beyond the built-in set describes those too. A schema baked at release time could not.
/// </para>
/// </remarks>
public sealed class ConfigSchemaGenerator(ICheckRegistry checkRegistry)
{
    private const string SchemaDialect = "https://json-schema.org/draft/2020-12/schema";

    public string Generate()
    {
        var root = new JsonObject
        {
            ["$schema"] = SchemaDialect,
            ["$id"] = "https://piro.dev/schema/piro.schema.json",
            ["title"] = "Piro configuration",
            ["description"] =
                "Services and checks as code. Anything this file does not declare, it does not touch.",
            ["type"] = "object",
            ["required"] = new JsonArray("version"),
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["version"] = new JsonObject
                {
                    ["const"] = 1,
                    ["description"] = "Format version. Only 1 is understood.",
                },
                ["services"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = "The services this file declares.",
                    ["items"] = ServiceSchema(),
                },
            },
        };

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }

    private JsonObject ServiceSchema() => new()
    {
        ["type"] = "object",
        ["required"] = new JsonArray("slug", "name"),
        ["additionalProperties"] = false,
        ["properties"] = new JsonObject
        {
            ["slug"] = Slug("Unique identifier. Immutable: renaming it is a delete and a create."),
            ["name"] = Text("Display name."),
            ["description"] = Text("Optional description."),
            ["is_hidden"] = Bool("Exclude this service from the public status page."),
            ["display_order"] = Integer("Position on the status page, ascending."),
            ["default_status"] = EnumOf<ServiceStatus>("Status shown before any check has reported."),
            ["checks"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = CheckSchema(),
            },
        },
    };

    private JsonObject CheckSchema()
    {
        // Only checks that can actually be declared in YAML. A type requiring an integration is
        // rejected by the validator (§2), so offering it here would autocomplete a file that can
        // never apply.
        var declarable = checkRegistry.All
            .Where(c => c.Manifest.RequiredIntegration is null)
            .Where(c => Enum.TryParse<CheckType>(c.CheckId, out _))
            .OrderBy(c => c.CheckId, StringComparer.Ordinal)
            .ToList();

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("slug", "name", "type", "cron"),
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["slug"] = Slug("Unique within its service. Immutable."),
                ["name"] = Text("Display name."),
                ["description"] = Text("Optional description."),
                ["type"] = new JsonObject
                {
                    ["description"] = "Check type. Immutable: changing it is a delete and a create.",
                    ["enum"] = new JsonArray([.. declarable.Select(c => (JsonNode)c.CheckId)]),
                },
                ["cron"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Cron expression. Must not run more often than every minute.",
                },
                ["is_active"] = Bool("Whether the check runs. Setting false unschedules it."),
                ["required_worker_tags"] = new JsonObject
                {
                    ["description"] = "Worker tags a runner must carry, as key/value pairs.",
                    ["oneOf"] = new JsonArray(
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["additionalProperties"] = new JsonObject
                            {
                                ["type"] = new JsonArray("string", "null"),
                            },
                        },
                        new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["type"] = "string" },
                        }),
                },
                ["alert_configs"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] =
                        "Alert rules, identified by dimension. Declaring this list replaces the "
                        + "check's rules; omitting it leaves them untouched.",
                    ["items"] = AlertConfigSchema(declarable),
                },
                // Constrained per type by the conditionals below, so the base is left open.
                ["type_data"] = new JsonObject
                {
                    ["type"] = "object",
                    ["description"] = "Type-specific configuration. Its shape depends on `type`.",
                },
            },
        };

        var conditionals = new JsonArray();
        foreach (var check in declarable)
            conditionals.Add(TypeDataConditional(check));

        if (conditionals.Count > 0) schema["allOf"] = conditionals;

        return schema;
    }

    /// <summary>
    /// One <c>if/then</c> pair binding a check type to the shape of its <c>type_data</c>. This is
    /// what makes an editor complete the right fields for the type on the line above.
    /// </summary>
    private static JsonObject TypeDataConditional(ICheck check)
    {
        var fields = ConfigSchemaBuilder.For(check.Manifest.ConfigType);

        return new JsonObject
        {
            ["if"] = new JsonObject
            {
                // `required` matters: without it the branch also matches a check that omits `type`,
                // and every branch would apply at once.
                ["properties"] = new JsonObject
                {
                    ["type"] = new JsonObject { ["const"] = check.CheckId },
                },
                ["required"] = new JsonArray("type"),
            },
            ["then"] = new JsonObject
            {
                ["properties"] = new JsonObject
                {
                    ["type_data"] = ObjectSchema(fields, check.Manifest.Label),
                },
            },
        };
    }

    /// <summary>Builds an object schema from a reflected field list.</summary>
    private static JsonObject ObjectSchema(IReadOnlyList<ConfigFieldSchemaDto> fields, string? title)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var field in fields)
        {
            properties[field.Key] = FieldSchema(field);

            // A conditionally-visible field cannot be globally required, or a file that legitimately
            // omits it (an HTTP body on a GET) would fail validation.
            if (field.Required && field.VisibleWhen is null && !field.IsGenerated)
                required.Add(field.Key);
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
        };

        if (title is not null) schema["title"] = $"{title} configuration";
        if (required.Count > 0) schema["required"] = required;

        return schema;
    }

    private static JsonObject FieldSchema(ConfigFieldSchemaDto field)
    {
        var schema = field.Type switch
        {
            ConfigFieldType.Number => new JsonObject { ["type"] = "number" },
            ConfigFieldType.Boolean => new JsonObject { ["type"] = "boolean" },

            ConfigFieldType.StringList => new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },

            ConfigFieldType.KeyValue => new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = new JsonObject { ["type"] = "string" },
            },

            ConfigFieldType.ObjectArray => new JsonObject
            {
                ["type"] = "array",
                ["items"] = field.ItemSchema is { } items
                    ? ObjectSchema(items, null)
                    : new JsonObject { ["type"] = "object" },
            },

            ConfigFieldType.Url => new JsonObject { ["type"] = "string", ["format"] = "uri" },
            ConfigFieldType.Email => new JsonObject { ["type"] = "string", ["format"] = "email" },

            _ => new JsonObject { ["type"] = "string" },
        };

        // An options list is an enum on the wire regardless of how the form renders it.
        if (field.Options is { Count: > 0 } options)
            schema["enum"] = new JsonArray([.. options.Select(o => (JsonNode)o)]);

        var description = Describe(field);
        if (description is not null) schema["description"] = description;

        if (field.Default is { } value && ToNode(value) is { } node) schema["default"] = node;

        return schema;
    }

    private static string? Describe(ConfigFieldSchemaDto field)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(field.HelpText)) parts.Add(field.HelpText);
        else if (!string.IsNullOrWhiteSpace(field.Label)) parts.Add(field.Label);

        // Surfaced because it explains why an otherwise-required field may be absent.
        if (field.VisibleWhen is { } visible)
            parts.Add($"Applies when {visible.Field} is {string.Join(" or ", visible.Values)}.");

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private JsonObject AlertConfigSchema(IReadOnlyList<ICheck> declarable)
    {
        // Every dimension any declarable check exposes. Narrowing per check type would need a second
        // conditional chain nested inside the first; the server rejects a dimension the check does
        // not declare, so the editor offering the union is a reasonable trade.
        var dimensions = declarable
            .SelectMany(c => c.Manifest.Dimensions.Select(d => d.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("dimension", "alert_value"),
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["dimension"] = new JsonObject
                {
                    ["description"] = "The dimension this rule watches. One rule per dimension.",
                    ["enum"] = new JsonArray([.. dimensions.Select(d => (JsonNode)d)]),
                },
                ["alert_value"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] =
                        "A status name for the Status dimension, otherwise a numeric threshold.",
                },
                ["failure_threshold"] = PositiveInteger("Consecutive failing cycles before firing."),
                ["success_threshold"] = PositiveInteger("Consecutive healthy cycles before resolving."),
                ["min_failing_regions"] = PositiveInteger("Regions that must fail within one cycle."),
                ["description"] = Text("Optional description."),
                ["is_active"] = Bool("Whether the rule is evaluated."),
                ["severity"] = EnumOf<AlertSeverity>("Severity raised when the rule fires."),
            },
        };
    }

    // ── Small builders ──────────────────────────────────────────────────────

    private static JsonObject Slug(string description) => new()
    {
        ["type"] = "string",
        ["pattern"] = "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        ["description"] = description,
    };

    private static JsonObject Text(string description) =>
        new() { ["type"] = "string", ["description"] = description };

    private static JsonObject Bool(string description) =>
        new() { ["type"] = "boolean", ["description"] = description };

    private static JsonObject Integer(string description) =>
        new() { ["type"] = "integer", ["description"] = description };

    private static JsonObject PositiveInteger(string description) =>
        new() { ["type"] = "integer", ["minimum"] = 1, ["description"] = description };

    private static JsonObject EnumOf<T>(string description) where T : struct, Enum => new()
    {
        ["description"] = description,
        ["enum"] = new JsonArray([.. Enum.GetNames<T>().Select(n => (JsonNode)n)]),
    };

    private static JsonNode? ToNode(object value) => value switch
    {
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        _ => null,
    };
}
