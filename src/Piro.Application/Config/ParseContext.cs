using Piro.Application.DTOs;
using YamlDotNet.RepresentationModel;

namespace Piro.Application.Config;

/// <summary>One key/value pair being read, carrying the YAML pointer that locates it.</summary>
/// <param name="Name">The key as written, e.g. <c>cron</c>.</param>
/// <param name="Pointer">Dotted path from the document root, e.g. <c>services[2].checks[0].cron</c>.</param>
internal readonly record struct ParseKey(string Name, string Pointer, YamlNode Value);

/// <summary>
/// Reads typed values out of a YAML mapping, recording a located error instead of throwing when a
/// value has the wrong shape. Collecting errors rather than failing on the first is what lets a user
/// fix a file in one pass instead of ten round-trips (RFC 0019 §4.3).
/// </summary>
internal sealed class ParseContext(string path, List<ConfigValidationError> errors)
{
    /// <summary>Invokes <paramref name="handle"/> for each key of <paramref name="node"/>.</summary>
    public void ForEachKey(YamlMappingNode node, string parentPointer, Action<ParseKey> handle)
    {
        foreach (var (keyNode, valueNode) in node.Children)
        {
            if (keyNode is not YamlScalarNode { Value: { } name })
            {
                Add("Keys must be plain strings.", keyNode);
                continue;
            }

            var pointer = string.IsNullOrEmpty(parentPointer) ? name : $"{parentPointer}.{name}";
            handle(new ParseKey(name, pointer, valueNode));
        }
    }

    /// <summary>Reads a sequence of mappings, giving each element an indexed pointer.</summary>
    public List<T> Sequence<T>(ParseKey key, Func<ParseContext, YamlMappingNode, string, T> parseItem)
    {
        var items = new List<T>();

        // An explicitly empty sequence is legitimate ("this service has no checks"), but a scalar or
        // mapping here means the author meant something else.
        if (key.Value is YamlScalarNode { Value: null or "" }) return items;

        if (key.Value is not YamlSequenceNode sequence)
        {
            Add($"'{key.Name}' must be a list.", key.Value, key.Pointer);
            return items;
        }

        for (var i = 0; i < sequence.Children.Count; i++)
        {
            if (sequence.Children[i] is not YamlMappingNode element)
            {
                Add($"'{key.Name}[{i}]' must be a mapping.", sequence.Children[i], $"{key.Pointer}[{i}]");
                continue;
            }
            items.Add(parseItem(this, element, $"{key.Pointer}[{i}]"));
        }

        return items;
    }

    public string? String(ParseKey key)
    {
        if (key.Value is YamlScalarNode { Value: { } value }) return value;
        Add($"'{key.Name}' must be a string.", key.Value, key.Pointer);
        return null;
    }

    public int? Int(ParseKey key)
    {
        if (key.Value is YamlScalarNode { Value: { } raw } && int.TryParse(raw, out var value))
            return value;
        Add($"'{key.Name}' must be a whole number.", key.Value, key.Pointer);
        return null;
    }

    public bool? Bool(ParseKey key)
    {
        if (key.Value is YamlScalarNode { Value: { } raw } && bool.TryParse(raw, out var value))
            return value;
        Add($"'{key.Name}' must be true or false.", key.Value, key.Pointer);
        return null;
    }

    /// <summary>
    /// Reads a key/value tag set. Accepts a mapping (<c>piro:region: eu-west</c>) and also a bare list
    /// of keys, since a key-only flag carries no value and writing it as a list is the natural thing
    /// to reach for.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? StringMapping(ParseKey key)
    {
        var result = new Dictionary<string, string?>();

        switch (key.Value)
        {
            case YamlMappingNode mapping:
                foreach (var (keyNode, valueNode) in mapping.Children)
                {
                    if (keyNode is not YamlScalarNode { Value: { } name })
                    {
                        Add($"'{key.Name}' keys must be strings.", keyNode, key.Pointer);
                        continue;
                    }
                    result[name] = valueNode is YamlScalarNode { Value: { } v and not "null" and not "~" and not "" }
                        ? v
                        : null;
                }
                return result;

            case YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    if (sequence.Children[i] is YamlScalarNode { Value: { } name })
                        result[name] = null;
                    else
                        Add($"'{key.Name}[{i}]' must be a string.", sequence.Children[i], $"{key.Pointer}[{i}]");
                }
                return result;

            default:
                Add($"'{key.Name}' must be a mapping of tag keys to values, or a list of tag keys.",
                    key.Value, key.Pointer);
                return null;
        }
    }

    /// <summary>
    /// Reads an arbitrary mapping (a check's <c>type_data</c>), preserving YAML's scalar types so the
    /// JSON it becomes has real numbers and booleans rather than quoted strings.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Mapping(ParseKey key)
    {
        if (key.Value is not YamlMappingNode mapping)
        {
            Add($"'{key.Name}' must be a mapping.", key.Value, key.Pointer);
            return null;
        }
        return ToDictionary(mapping);
    }

    private Dictionary<string, object?> ToDictionary(YamlMappingNode mapping)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (keyNode, valueNode) in mapping.Children)
            if (keyNode is YamlScalarNode { Value: { } name })
                result[name] = ToValue(valueNode);
        return result;
    }

    private object? ToValue(YamlNode node) => node switch
    {
        YamlMappingNode mapping => ToDictionary(mapping),
        YamlSequenceNode sequence => sequence.Children.Select(ToValue).ToList(),
        YamlScalarNode scalar => Scalar(scalar),
        _ => null,
    };

    /// <summary>
    /// Recovers a scalar's intended type. A quoted scalar stays a string even when it looks numeric,
    /// so a config field like a zero-padded code or a version "1.0" survives the round-trip intact.
    /// </summary>
    private static object? Scalar(YamlScalarNode node)
    {
        if (node.Value is not { } raw) return null;
        if (node.Style is YamlDotNet.Core.ScalarStyle.SingleQuoted or YamlDotNet.Core.ScalarStyle.DoubleQuoted)
            return raw;

        if (raw is "null" or "~" or "") return null;
        if (bool.TryParse(raw, out var b)) return b;
        if (long.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var l)) return l;
        if (double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        return raw;
    }

    public void UnknownKey(ParseKey key, params string[] known) =>
        Add($"Unknown field '{key.Name}'. Expected one of: {string.Join(", ", known)}.",
            key.Value, key.Pointer);

    public void Error(ParseKey key, string message) => Add(message, key.Value, key.Pointer);

    private void Add(string message, YamlNode at, string? pointer = null) =>
        errors.Add(new ConfigValidationError(message, path, (int)at.Start.Line, (int)at.Start.Column, pointer));
}
