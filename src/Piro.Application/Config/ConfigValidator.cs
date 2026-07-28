using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Checks.Abstractions;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.Application.Config;

/// <summary>
/// Validates parsed config documents before anything is written (RFC 0019 §4.3). Everything is
/// checked up front and every failure is collected, so a user fixes a file in one pass rather than
/// discovering errors one apply at a time — and so a document can never half-apply.
/// </summary>
public sealed class ConfigValidator(ICheckRegistry checkRegistry, ICronIntervalCalculator cronInterval)
{
    private static readonly JsonSerializerOptions TypeDataJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Slug format, matching what the admin panel produces: lowercase, digits, dashes.</summary>
    private static readonly System.Text.RegularExpressions.Regex SlugPattern =
        new("^[a-z0-9]+(?:-[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Validates every document and returns the services that passed, keyed by slug. A document that
    /// contributes errors still contributes its well-formed resources to the returned map, because the
    /// caller aborts on any error at all — the map is only consumed when the error list is empty.
    /// </summary>
    public IReadOnlyList<ValidatedService> Validate(
        IReadOnlyList<(ConfigDocumentSource Source, ConfigDocument Document)> documents,
        List<ConfigValidationError> errors)
    {
        var services = new List<ValidatedService>();

        // Where each slug was first declared, so a collision names both files rather than reporting a
        // bare "duplicate" against a directory the user has to search by hand (§4.6).
        var serviceOrigins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (source, document) in documents)
        {
            ValidateVersion(source, document, errors);

            for (var i = 0; i < document.Services.Count; i++)
            {
                var node = document.Services[i];
                var pointer = $"services[{i}]";
                var validated = ValidateService(source, node, pointer, errors);
                if (validated is null) continue;

                if (serviceOrigins.TryGetValue(validated.Slug, out var firstPath))
                {
                    errors.Add(new ConfigValidationError(
                        $"Service '{validated.Slug}' is declared twice — first in {firstPath}, again here. "
                        + "Files are concatenated, not merged.",
                        source.Path, node.Line, null, $"{pointer}.slug"));
                    continue;
                }

                serviceOrigins[validated.Slug] = source.Path;
                services.Add(validated);
            }
        }

        return services;
    }

    private static void ValidateVersion(ConfigDocumentSource source, ConfigDocument document, List<ConfigValidationError> errors)
    {
        if (document.Version is null)
            errors.Add(new ConfigValidationError(
                "Missing required top-level 'version: 1'.", source.Path, 1, 1, "version"));
        else if (document.Version != 1)
            errors.Add(new ConfigValidationError(
                $"Unsupported config version {document.Version}. This Piro understands version 1.",
                source.Path, 1, 1, "version"));
    }

    private ValidatedService? ValidateService(
        ConfigDocumentSource source, ConfigServiceNode node, string pointer, List<ConfigValidationError> errors)
    {
        void Error(string message, string? field = null, int? line = null) =>
            errors.Add(new ConfigValidationError(
                message, source.Path, line ?? node.Line, null,
                field is null ? pointer : $"{pointer}.{field}"));

        // Without a slug there is no identity to diff against, so the node cannot be reconciled at all.
        if (string.IsNullOrWhiteSpace(node.Slug))
        {
            Error("A service must declare a 'slug'.", "slug");
            return null;
        }

        if (!SlugPattern.IsMatch(node.Slug))
            Error($"'{node.Slug}' is not a valid slug — use lowercase letters, digits and dashes.", "slug");

        if (string.IsNullOrWhiteSpace(node.Name))
            Error("A service must declare a 'name'.", "name");

        ServiceStatus? defaultStatus = null;
        if (node.DefaultStatus is { } rawStatus)
        {
            if (Enum.TryParse<ServiceStatus>(rawStatus, ignoreCase: true, out var parsed))
                defaultStatus = parsed;
            else
                Error($"'{rawStatus}' is not a valid status. Expected one of: {Names<ServiceStatus>()}.",
                    "default_status");
        }

        var checks = new List<ValidatedCheck>();
        var checkOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < node.Checks.Count; i++)
        {
            var checkNode = node.Checks[i];
            var checkPointer = $"{pointer}.checks[{i}]";
            var check = ValidateCheck(source, checkNode, checkPointer, errors);
            if (check is null) continue;

            if (!checkOrigins.Add(check.Slug))
            {
                errors.Add(new ConfigValidationError(
                    $"Check '{check.Slug}' is declared twice in service '{node.Slug}'.",
                    source.Path, checkNode.Line, null, $"{checkPointer}.slug"));
                continue;
            }

            checks.Add(check);
        }

        return new ValidatedService(node.Slug, node, defaultStatus, checks, source.Path);
    }

    private ValidatedCheck? ValidateCheck(
        ConfigDocumentSource source, ConfigCheckNode node, string pointer, List<ConfigValidationError> errors)
    {
        void Error(string message, string? field = null) =>
            errors.Add(new ConfigValidationError(
                message, source.Path, node.Line, null, field is null ? pointer : $"{pointer}.{field}"));

        if (string.IsNullOrWhiteSpace(node.Slug))
        {
            Error("A check must declare a 'slug'.", "slug");
            return null;
        }

        if (!SlugPattern.IsMatch(node.Slug))
            Error($"'{node.Slug}' is not a valid slug — use lowercase letters, digits and dashes.", "slug");

        if (string.IsNullOrWhiteSpace(node.Name))
            Error("A check must declare a 'name'.", "name");

        // Type resolves through the registry, not the enum: the registry is what actually has an
        // implementation, so a declared-but-unregistered type (Heartbeat) is correctly rejected.
        if (string.IsNullOrWhiteSpace(node.Type))
        {
            Error("A check must declare a 'type'.", "type");
            return null;
        }

        var impl = checkRegistry.All.FirstOrDefault(
            c => string.Equals(c.CheckId, node.Type, StringComparison.OrdinalIgnoreCase));
        if (impl is null)
        {
            Error($"'{node.Type}' is not a known check type. Registered types: "
                + $"{string.Join(", ", checkRegistry.All.Select(c => c.CheckId).Order())}.", "type");
            return null;
        }

        if (!Enum.TryParse<CheckType>(impl.CheckId, out var checkType))
        {
            Error($"Check type '{impl.CheckId}' has no corresponding stored type.", "type");
            return null;
        }

        // A check needing an integration needs a credential, and a credential cannot come from a file
        // in a Git repository (§2). Said plainly rather than skipped silently.
        if (impl.Manifest.RequiredIntegration is { } required)
            Error($"{impl.Manifest.Label} checks require the '{required}' integration, which cannot be "
                + "declared in YAML. Create this check in the admin panel.", "type");

        if (node.RequiredWorkerTags is { Count: > 0 } && impl.Manifest.SingleRegionOnly)
            Error($"{impl.Manifest.Label} checks run in a single region and cannot require worker tags.",
                "required_worker_tags");

        if (string.IsNullOrWhiteSpace(node.Cron))
            Error("A check must declare a 'cron'.", "cron");
        else if (!cronInterval.IsValid(node.Cron))
            Error($"'{node.Cron}' is not a valid cron expression.", "cron");

        var typeDataJson = SerializeTypeData(node.TypeData);
        ValidateTypeData(impl, typeDataJson, Error);

        if (node.Cron is { } cron && cronInterval.IsValid(cron))
            ValidateInterval(impl, cron, typeDataJson, Error);

        var alerts = ValidateAlertConfigs(source, node, impl, pointer, errors);

        return new ValidatedCheck(node.Slug, node, checkType, typeDataJson, alerts);
    }

    /// <summary>
    /// Binds <c>type_data</c> to the check's own config type and runs its DataAnnotations, the same
    /// deserialize-and-validate shape used for integration action input. Today the write path stores
    /// the blob verbatim and a malformed config only fails at execution time (§4.3).
    /// </summary>
    private static void ValidateTypeData(ICheck impl, string typeDataJson, Action<string, string?> error)
    {
        object? config;
        try
        {
            config = JsonSerializer.Deserialize(typeDataJson, impl.Manifest.ConfigType, TypeDataJson);
        }
        catch (JsonException ex)
        {
            error($"Invalid config for a {impl.Manifest.Label} check: {ex.Message}", "type_data");
            return;
        }

        if (config is null)
        {
            error($"A {impl.Manifest.Label} check needs a 'type_data' mapping.", "type_data");
            return;
        }

        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(config, new ValidationContext(config), results, validateAllProperties: true))
            return;

        foreach (var result in results)
        {
            var member = result.MemberNames.FirstOrDefault();
            error(result.ErrorMessage ?? "Invalid value.",
                member is null ? "type_data" : $"type_data.{ToYamlKey(member)}");
        }
    }

    /// <summary>
    /// The global one-minute floor, the per-type floor from the manifest, and timeout-below-interval —
    /// the same rules <c>CheckAppService.EnsureScheduleWithinBounds</c> applies on the CRUD path.
    /// </summary>
    private void ValidateInterval(ICheck impl, string cron, string typeDataJson, Action<string, string?> error)
    {
        if (cronInterval.SmallestInterval(cron) is not { } interval) return;

        if (interval < TimeSpan.FromMinutes(1))
        {
            error("Check interval must be at least 1 minute.", "cron");
            return;
        }

        var min = TimeSpan.FromSeconds(impl.Manifest.DefaultIntervalSeconds);
        if (interval < min)
        {
            error($"{impl.Manifest.Label} checks must run no more often than every "
                + $"{min.TotalMinutes:0} minute(s).", "cron");
            return;
        }

        if (TimeoutOf(impl, typeDataJson) is { } timeout && timeout >= interval)
            error($"The check timeout ({timeout.TotalSeconds:0}s) must be shorter than its interval "
                + $"({interval.TotalSeconds:0}s).", "type_data");
    }

    private static TimeSpan? TimeoutOf(ICheck impl, string typeDataJson)
    {
        var prop = impl.Manifest.ConfigType.GetProperty("TimeoutMs");
        if (prop is null) return null;

        object? config;
        try { config = JsonSerializer.Deserialize(typeDataJson, impl.Manifest.ConfigType, TypeDataJson); }
        catch (JsonException) { return null; }
        if (config is null) return null;

        return prop.GetValue(config) switch
        {
            int ms => TimeSpan.FromMilliseconds(ms),
            long ms => TimeSpan.FromMilliseconds(ms),
            double ms => TimeSpan.FromMilliseconds(ms),
            _ => null,
        };
    }

    private static List<ValidatedAlertConfig> ValidateAlertConfigs(
        ConfigDocumentSource source, ConfigCheckNode node, ICheck impl, string pointer,
        List<ConfigValidationError> errors)
    {
        var alerts = new List<ValidatedAlertConfig>();
        if (node.AlertConfigs is not { } declared) return alerts;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < declared.Count; i++)
        {
            var alert = declared[i];
            var alertPointer = $"{pointer}.alert_configs[{i}]";

            void Error(string message, string? field = null) =>
                errors.Add(new ConfigValidationError(
                    message, source.Path, alert.Line, null,
                    field is null ? alertPointer : $"{alertPointer}.{field}"));

            if (string.IsNullOrWhiteSpace(alert.Dimension))
            {
                Error("An alert config must declare a 'dimension'.", "dimension");
                continue;
            }

            // The dimension must be one the check declares — the same guard ResolveSpec applies — and
            // it doubles as the rule's identity, so an unknown one cannot be matched or created.
            var spec = impl.Manifest.Dimensions.FirstOrDefault(
                d => string.Equals(d.Name, alert.Dimension, StringComparison.OrdinalIgnoreCase));
            if (spec is null)
            {
                Error($"'{alert.Dimension}' is not an alert dimension of a {impl.Manifest.Label} check. "
                    + $"Available: {string.Join(", ", impl.Manifest.Dimensions.Select(d => d.Name))}.",
                    "dimension");
                continue;
            }

            if (!seen.Add(spec.Name))
            {
                Error($"Dimension '{spec.Name}' has more than one alert config on this check. "
                    + "Alert rules are identified by their dimension, so each may appear once.", "dimension");
                continue;
            }

            if (string.IsNullOrWhiteSpace(alert.AlertValue))
                Error("An alert config must declare an 'alert_value'.", "alert_value");
            else
                ValidateAlertValue(spec, alert.AlertValue, Error);

            AlertSeverity? severity = null;
            if (alert.Severity is { } rawSeverity)
            {
                if (Enum.TryParse<AlertSeverity>(rawSeverity, ignoreCase: true, out var parsed))
                    severity = parsed;
                else
                    Error($"'{rawSeverity}' is not a valid severity. Expected one of: "
                        + $"{Names<AlertSeverity>()}.", "severity");
            }

            ValidatePositive(alert.FailureThreshold, "failure_threshold", Error);
            ValidatePositive(alert.SuccessThreshold, "success_threshold", Error);
            ValidatePositive(alert.MinFailingRegions, "min_failing_regions", Error);

            alerts.Add(new ValidatedAlertConfig(spec, alert, severity));
        }

        return alerts;
    }

    /// <summary>
    /// An Equality dimension compares against a status name; a Threshold dimension against a number.
    /// Catching this here matters because the value is stored as a string and otherwise only fails
    /// when the evaluator tries to parse it, long after the apply reported success.
    /// </summary>
    private static void ValidateAlertValue(DimensionSpec spec, string value, Action<string, string?> error)
    {
        if (spec.Comparison == DimensionComparison.Equality)
        {
            if (!Enum.TryParse<ServiceStatus>(value, ignoreCase: true, out _))
                error($"'{value}' is not a valid status for the {spec.Name} dimension. "
                    + $"Expected one of: {Names<ServiceStatus>()}.", "alert_value");
        }
        else if (!double.TryParse(value, System.Globalization.NumberStyles.Float,
                     System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            error($"The {spec.Name} dimension compares numerically, so 'alert_value' must be a number.",
                "alert_value");
        }
    }

    private static void ValidatePositive(int? value, string field, Action<string, string?> error)
    {
        if (value is { } n && n < 1) error($"'{field}' must be at least 1.", field);
    }

    private static string Names<T>() where T : struct, Enum => string.Join(", ", Enum.GetNames<T>());

    /// <summary>Renders a CLR property name as the yaml key a user would have written.</summary>
    private static string ToYamlKey(string member) =>
        char.ToLowerInvariant(member[0]) + member[1..];

    /// <summary>Serializes a parsed <c>type_data</c> mapping to the JSON stored on the check.</summary>
    public static string SerializeTypeData(IReadOnlyDictionary<string, object?>? typeData) =>
        typeData is null ? "{}" : JsonSerializer.Serialize(typeData);
}

/// <summary>A service node that passed validation, paired with what validation resolved.</summary>
public sealed record ValidatedService(
    string Slug,
    ConfigServiceNode Node,
    ServiceStatus? DefaultStatus,
    IReadOnlyList<ValidatedCheck> Checks,
    string Path);

/// <summary>A check node that passed validation, with its resolved type and serialized config.</summary>
public sealed record ValidatedCheck(
    string Slug,
    ConfigCheckNode Node,
    CheckType Type,
    string TypeDataJson,
    IReadOnlyList<ValidatedAlertConfig> AlertConfigs);

/// <summary>
/// An alert rule that passed validation. Carries the resolved <see cref="DimensionSpec"/> because
/// <c>Comparison</c> and <c>Direction</c> are copied from it rather than declared in the file.
/// </summary>
public sealed record ValidatedAlertConfig(
    DimensionSpec Spec,
    ConfigAlertConfigNode Node,
    AlertSeverity? Severity);
