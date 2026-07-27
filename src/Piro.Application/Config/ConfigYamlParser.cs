using Piro.Application.DTOs;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Piro.Application.Config;

/// <summary>
/// Parses a <c>piro.yaml</c> source into a <see cref="ConfigDocument"/> (RFC 0019 §4.1).
/// </summary>
/// <remarks>
/// Walks the YAML node graph by hand rather than using YamlDotNet's object deserializer, for two
/// reasons the deserializer cannot give: every node keeps the line it came from, so errors and plan
/// entries point at the text the user actually wrote; and an unrecognised key is an error rather
/// than being silently dropped, which matters because a typo like <c>crons:</c> would otherwise mean
/// "field not declared" and, under patch semantics, silently leave the real value untouched.
/// </remarks>
public static class ConfigYamlParser
{
    /// <summary>
    /// Parses one document, appending any failures to <paramref name="errors"/>. Returns null only
    /// when the file could not be parsed at all; a document that parsed but has invalid content is
    /// returned so later validation can report everything at once rather than one error per round-trip.
    /// </summary>
    public static ConfigDocument? Parse(ConfigDocumentSource source, List<ConfigValidationError> errors)
    {
        YamlStream stream = new();
        try
        {
            stream.Load(new StringReader(source.Content));
        }
        catch (YamlException ex)
        {
            errors.Add(new ConfigValidationError(
                CleanYamlMessage(ex), source.Path, (int)ex.Start.Line, (int)ex.Start.Column));
            return null;
        }

        // An empty file is a mistake worth naming: silently contributing nothing is exactly the
        // failure mode that makes --prune delete resources the user believed were declared (§4.6).
        if (stream.Documents.Count == 0)
        {
            errors.Add(new ConfigValidationError("The file is empty.", source.Path, 1, 1));
            return null;
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            errors.Add(new ConfigValidationError(
                "The document root must be a mapping with a 'version' key.", source.Path, 1, 1));
            return null;
        }

        var ctx = new ParseContext(source.Path, errors);
        var document = new ConfigDocument();

        ctx.ForEachKey(root, "", key =>
        {
            switch (key.Name)
            {
                case "version":
                    document.Version = ctx.Int(key);
                    break;
                case "services":
                    document.Services = ctx.Sequence(key, ParseService);
                    break;
                default:
                    ctx.UnknownKey(key, "version", "services");
                    break;
            }
        });

        return document;
    }

    private static ConfigServiceNode ParseService(ParseContext ctx, YamlMappingNode node, string path)
    {
        var service = new ConfigServiceNode { Line = (int)node.Start.Line };

        ctx.ForEachKey(node, path, key =>
        {
            switch (key.Name)
            {
                case "slug": service.Slug = ctx.String(key); break;
                case "name": service.Name = ctx.String(key); break;
                case "description": service.Description = ctx.String(key); break;
                case "is_hidden": service.IsHidden = ctx.Bool(key); break;
                case "display_order": service.DisplayOrder = ctx.Int(key); break;
                case "image_url": service.ImageUrl = ctx.String(key); break;
                case "default_status": service.DefaultStatus = ctx.String(key); break;
                case "checks": service.Checks = ctx.Sequence(key, ParseCheck); break;
                default:
                    ctx.UnknownKey(key, "slug", "name", "description", "is_hidden",
                        "display_order", "image_url", "default_status", "checks");
                    break;
            }
        });

        return service;
    }

    private static ConfigCheckNode ParseCheck(ParseContext ctx, YamlMappingNode node, string path)
    {
        var check = new ConfigCheckNode { Line = (int)node.Start.Line };

        ctx.ForEachKey(node, path, key =>
        {
            switch (key.Name)
            {
                case "slug": check.Slug = ctx.String(key); break;
                case "name": check.Name = ctx.String(key); break;
                case "description": check.Description = ctx.String(key); break;
                case "type": check.Type = ctx.String(key); break;
                case "cron": check.Cron = ctx.String(key); break;
                case "is_active": check.IsActive = ctx.Bool(key); break;
                case "type_data": check.TypeData = ctx.Mapping(key); break;
                case "required_worker_tags": check.RequiredWorkerTags = ctx.StringMapping(key); break;
                case "alert_configs": check.AlertConfigs = ctx.Sequence(key, ParseAlertConfig); break;
                case "integration" or "integration_id":
                    // Named explicitly rather than falling through to "unknown key", because the
                    // reason is a deliberate boundary, not a typo: a file in a Git repository must
                    // never accumulate credentials or references to them (§2).
                    ctx.Error(key, "Checks cannot reference an integration from YAML — "
                        + "integrations hold credentials and are managed in the admin panel.");
                    break;
                default:
                    ctx.UnknownKey(key, "slug", "name", "description", "type", "cron", "is_active",
                        "type_data", "required_worker_tags", "alert_configs");
                    break;
            }
        });

        return check;
    }

    private static ConfigAlertConfigNode ParseAlertConfig(ParseContext ctx, YamlMappingNode node, string path)
    {
        var alert = new ConfigAlertConfigNode { Line = (int)node.Start.Line };

        ctx.ForEachKey(node, path, key =>
        {
            switch (key.Name)
            {
                case "dimension": alert.Dimension = ctx.String(key); break;
                case "alert_value": alert.AlertValue = ctx.String(key); break;
                case "failure_threshold": alert.FailureThreshold = ctx.Int(key); break;
                case "success_threshold": alert.SuccessThreshold = ctx.Int(key); break;
                case "min_failing_regions": alert.MinFailingRegions = ctx.Int(key); break;
                case "description": alert.Description = ctx.String(key); break;
                case "is_active": alert.IsActive = ctx.Bool(key); break;
                case "severity": alert.Severity = ctx.String(key); break;
                case "comparison" or "direction":
                    ctx.Error(key, $"'{key.Name}' is derived from the check's dimension spec and "
                        + "cannot be set in YAML.");
                    break;
                case "is_alerting":
                    ctx.Error(key, "'is_alerting' is live alert state, not configuration.");
                    break;
                default:
                    ctx.UnknownKey(key, "dimension", "alert_value", "failure_threshold",
                        "success_threshold", "min_failing_regions", "description", "is_active", "severity");
                    break;
            }
        });

        return alert;
    }

    /// <summary>YamlException messages are prefixed with a duplicated position; strip it.</summary>
    private static string CleanYamlMessage(YamlException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        var marker = message.LastIndexOf("): ", StringComparison.Ordinal);
        return marker >= 0 ? message[(marker + 3)..] : message;
    }
}
