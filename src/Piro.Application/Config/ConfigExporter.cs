using System.Globalization;
using System.Text;
using System.Text.Json;
using Piro.Application.Interfaces;
using Piro.Checks.Abstractions;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Config;

/// <summary>
/// Serializes the current instance into a v1 <c>piro.yaml</c> (RFC 0019 §4.8) — a bootstrap tool for
/// adopting config as code against a topology that already exists.
/// </summary>
/// <remarks>
/// Emitted by hand rather than through YamlDotNet's serializer so the output reads like a file a
/// person wrote: fields at their default are omitted, key order is stable, and the parts that cannot
/// round-trip are commented rather than dropped. Being honest about lossiness is the point — a user
/// who exports and then applies with <c>--prune</c> must not silently delete what merely failed to
/// serialize.
/// </remarks>
public sealed class ConfigExporter(
    IServiceRepository serviceRepository,
    ICheckRepository checkRepository,
    IAlertConfigRepository alertConfigRepository,
    ITagRepository tagRepository,
    ICheckRegistry checkRegistry)
{
    private const string SchemaComment =
        "# yaml-language-server: $schema=./piro.schema.json   # from `piro schema -o piro.schema.json`";

    public async Task<string> ExportAsync(CancellationToken ct = default)
    {
        var output = new StringBuilder();
        output.AppendLine(SchemaComment);
        output.AppendLine("version: 1");

        var services = (await serviceRepository.GetAllAsync(ct))
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Slug, StringComparer.Ordinal)
            .ToList();

        if (services.Count == 0)
        {
            output.AppendLine();
            output.AppendLine("services: []");
            return output.ToString();
        }

        output.AppendLine();
        output.AppendLine("services:");

        foreach (var service in services)
            await AppendServiceAsync(output, service, ct);

        return output.ToString();
    }

    private async Task AppendServiceAsync(StringBuilder output, Service service, CancellationToken ct)
    {
        output.Append("  - slug: ").AppendLine(Scalar(service.Slug));
        output.Append("    name: ").AppendLine(Scalar(service.Name));

        AppendIf(output, "    ", "description", service.Description);
        if (service.IsHidden) output.AppendLine("    is_hidden: true");
        if (service.DisplayOrder != 0)
            output.Append("    display_order: ").AppendLine(service.DisplayOrder.ToString(CultureInfo.InvariantCulture));
        if (service.DefaultStatus != ServiceStatus.NO_DATA)
            output.Append("    default_status: ").AppendLine(service.DefaultStatus.ToString());

        if (service.EscalationPolicyId is not null)
            output.AppendLine("    # escalation_policy is managed in the admin panel and is not exported.");

        var checks = (await checkRepository.GetByServiceIdAsync(service.Id, ct))
            .OrderBy(c => c.Slug, StringComparer.Ordinal)
            .ToList();

        if (checks.Count == 0)
        {
            output.AppendLine();
            return;
        }

        output.AppendLine("    checks:");
        foreach (var check in checks)
            await AppendCheckAsync(output, check, ct);

        output.AppendLine();
    }

    private async Task AppendCheckAsync(StringBuilder output, Check check, CancellationToken ct)
    {
        // A check bound to an integration cannot be represented, since integrations hold credentials
        // (§2). Commented out rather than dropped, so an apply --prune over the exported file does
        // not delete a check that merely failed to serialize (§4.8).
        //
        // Two ways a check can be integration-bound, and both must be caught or export emits a file
        // that fails its own plan: the Check.IntegrationId column, and a check type whose manifest
        // declares RequiredIntegration and carries the reference inside its own type_data (the GCP
        // Cloud Run Job check). The manifest is the same signal ConfigValidator rejects on, so
        // reading it here is what keeps the two sides agreeing.
        var manifest = checkRegistry.Find(check.Type.ToString())?.Manifest;
        if (check.IntegrationId is not null || manifest?.RequiredIntegration is not null)
        {
            var reason = manifest?.RequiredIntegration is { } required
                ? $"requires the '{required}' integration"
                : "uses an integration";

            output.AppendLine($"      # Check '{check.Slug}' {reason} and cannot be expressed in YAML.");
            output.AppendLine("      # It is managed in the admin panel. Removing this comment is not enough to");
            output.AppendLine("      # adopt it here, and a `piro apply --prune` against this file would delete it.");
            return;
        }

        output.Append("      - slug: ").AppendLine(Scalar(check.Slug));
        output.Append("        name: ").AppendLine(Scalar(check.Name));
        AppendIf(output, "        ", "description", check.Description);
        output.Append("        type: ").AppendLine(check.Type.ToString());
        output.Append("        cron: ").AppendLine(Scalar(check.Cron));
        if (!check.IsActive) output.AppendLine("        is_active: false");

        AppendTypeData(output, check);
        await AppendWorkerTagsAsync(output, check, ct);
        await AppendAlertConfigsAsync(output, check, ct);
    }

    private void AppendTypeData(StringBuilder output, Check check)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(check.TypeDataJson) ? "{}" : check.TypeDataJson);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Stored config that is not even JSON predates any validation; flag it rather than emit
            // a file that fails its own plan (§8).
            output.AppendLine("        # This check's stored config is not valid JSON and was not exported.");
            return;
        }

        if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Any()) return;

        // Config the check's own type would reject exists on instances predating type_data validation.
        // Exporting it silently produces a file that fails its own plan, so say so in place.
        if (checkRegistry.Find(check.Type.ToString()) is { } impl && !BindsCleanly(impl, check.TypeDataJson))
            output.AppendLine("        # This config does not match the check type's schema and may fail validation.");

        output.AppendLine("        type_data:");
        foreach (var property in root.EnumerateObject())
            AppendJsonValue(output, "          ", property.Name, property.Value);
    }

    private static bool BindsCleanly(ICheck impl, string typeDataJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize(typeDataJson, impl.Manifest.ConfigType,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (config is null) return false;

            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            return System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                config, new System.ComponentModel.DataAnnotations.ValidationContext(config), results, true);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task AppendWorkerTagsAsync(StringBuilder output, Check check, CancellationToken ct)
    {
        var required = await tagRepository.GetRequiredWorkerTagsAsync(check.Id, ct);
        if (required.Count == 0) return;

        output.AppendLine("        required_worker_tags:");
        foreach (var tag in required.OrderBy(t => t.Tag.Key, StringComparer.Ordinal))
            output.Append("          ").Append(Scalar(tag.Tag.Key)).Append(": ")
                .AppendLine(tag.Value is null ? "null" : Scalar(tag.Value));
    }

    private async Task AppendAlertConfigsAsync(StringBuilder output, Check check, CancellationToken ct)
    {
        var alerts = (await alertConfigRepository.GetByCheckIdAsync(check.Id, ct))
            .OrderBy(a => a.Dimension, StringComparer.Ordinal)
            .ToList();
        if (alerts.Count == 0) return;

        output.AppendLine("        alert_configs:");
        foreach (var alert in alerts)
        {
            output.Append("          - dimension: ").AppendLine(Scalar(alert.Dimension));
            output.Append("            alert_value: ").AppendLine(Scalar(alert.AlertValue));
            if (alert.FailureThreshold != 1)
                output.Append("            failure_threshold: ").AppendLine(alert.FailureThreshold.ToString(CultureInfo.InvariantCulture));
            if (alert.SuccessThreshold != 1)
                output.Append("            success_threshold: ").AppendLine(alert.SuccessThreshold.ToString(CultureInfo.InvariantCulture));
            if (alert.MinFailingRegions != 1)
                output.Append("            min_failing_regions: ").AppendLine(alert.MinFailingRegions.ToString(CultureInfo.InvariantCulture));
            AppendIf(output, "            ", "description", alert.Description);
            if (!alert.IsActive) output.AppendLine("            is_active: false");
            if (alert.Severity != AlertSeverity.Warning)
                output.Append("            severity: ").AppendLine(alert.Severity.ToString());
        }
    }

    private static void AppendJsonValue(StringBuilder output, string indent, string key, JsonElement value)
    {
        switch (value.ValueKind)
        {
            // An empty object or array must be written in flow style. Emitting a bare "key:" with
            // nothing under it parses back as null, not as empty, so a re-applied export would
            // replace [] with null on every field the check left empty.
            case JsonValueKind.Object when !value.EnumerateObject().Any():
                output.Append(indent).Append(Scalar(key)).AppendLine(": {}");
                break;

            case JsonValueKind.Array when value.GetArrayLength() == 0:
                output.Append(indent).Append(Scalar(key)).AppendLine(": []");
                break;

            case JsonValueKind.Object:
                output.Append(indent).Append(Scalar(key)).AppendLine(":");
                foreach (var property in value.EnumerateObject())
                    AppendJsonValue(output, indent + "  ", property.Name, property.Value);
                break;

            case JsonValueKind.Array:
                output.Append(indent).Append(Scalar(key)).AppendLine(":");
                foreach (var item in value.EnumerateArray())
                    output.Append(indent).Append("  - ").AppendLine(InlineJson(item));
                break;

            default:
                output.Append(indent).Append(Scalar(key)).Append(": ").AppendLine(InlineJson(value));
                break;
        }
    }

    private static string InlineJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => Scalar(value.GetString()!),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        JsonValueKind.Number => value.GetRawText(),
        _ => JsonSerializer.Serialize(value),
    };

    private static void AppendIf(StringBuilder output, string indent, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            output.Append(indent).Append(key).Append(": ").AppendLine(Scalar(value));
    }

    /// <summary>
    /// Renders a string as a YAML scalar, quoting whenever a bare form would parse as something else
    /// — a number, a bool, null, or anything with structural characters. A cron like <c>* * * * *</c>
    /// must be quoted or it is a YAML alias.
    /// </summary>
    private static string Scalar(string value)
    {
        if (value.Length == 0) return "\"\"";

        var needsQuoting =
            value.Trim() != value
            || bool.TryParse(value, out _)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            || value is "null" or "~" or "yes" or "no" or "on" or "off" or "true" or "false"
            || value.IndexOfAny([':', '#', '-', '?', '*', '&', '!', '|', '>', '\'', '"', '%', '@', '`',
                '{', '}', '[', ']', ',', '\n', '\r', '\t']) >= 0;

        if (!needsQuoting) return value;

        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t") + "\"";
    }
}
