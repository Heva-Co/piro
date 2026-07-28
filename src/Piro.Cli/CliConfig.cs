namespace Piro.Cli;

/// <summary>One configured instance in <c>piro.config.yml</c>.</summary>
/// <param name="AdminUrl">
/// Where the admin panel is served, when it is not behind the same origin as the API. Only the
/// browser login needs it: the consent screen is a panel route, not an API one.
/// </param>
internal sealed record CliInstance(string Name, string? Url, string? Config, string? AdminUrl);

/// <summary>
/// The contents of a <c>piro.config.yml</c>, plus where it was found so errors can name the file.
/// </summary>
internal sealed record CliConfig(string? Current, IReadOnlyDictionary<string, CliInstance> Instances, string Path);

/// <summary>
/// Finds and parses <c>piro.config.yml</c> (RFC 0019 §4.6).
/// </summary>
/// <remarks>
/// Hand-parsed rather than via YamlDotNet: the schema is tiny and closed, and keeping the reflection-heavy
/// deserializer out of the binary is most of what keeps NativeAOT viable here. Only the two-level shape
/// below is understood — anything else is reported rather than guessed at.
/// </remarks>
internal static class CliConfigLoader
{
    public const string FileName = "piro.config.yml";
    private const string AltFileName = "piro.config.yaml";

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a config file, the way git and
    /// package managers resolve theirs, then falls back to <c>~/.piro/config.yml</c>. A repo-local file
    /// is what lets `piro plan` work with no arguments on CI and on a teammate's laptop.
    /// </summary>
    public static CliConfig? Find(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            foreach (var name in new[] { FileName, AltFileName })
            {
                var candidate = Path.Combine(directory.FullName, name);
                if (File.Exists(candidate)) return Parse(File.ReadAllLines(candidate), candidate);
            }
            directory = directory.Parent;
        }

        var home = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".piro", "config.yml");
        return File.Exists(home) ? Parse(File.ReadAllLines(home), home) : null;
    }

    /// <summary>
    /// Parses the closed shape below. Indentation defines nesting; comments and blank lines are skipped.
    /// <code>
    /// current: production
    /// instances:
    ///   production:
    ///     url: https://status.heva.co
    ///     config: ./piro/
    /// </code>
    /// </summary>
    public static CliConfig Parse(IReadOnlyList<string> lines, string path)
    {
        string? current = null;
        var instances = new Dictionary<string, CliInstance>(StringComparer.OrdinalIgnoreCase);

        var inInstances = false;
        string? instanceName = null;
        string? url = null;
        string? config = null;
        string? adminUrl = null;

        void Flush()
        {
            if (instanceName is not null)
                instances[instanceName] = new CliInstance(instanceName, url, config, adminUrl);
            instanceName = null;
            url = null;
            config = null;
            adminUrl = null;
        }

        foreach (var raw in lines)
        {
            var line = StripComment(raw);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var indent = line.Length - line.TrimStart().Length;
            var trimmed = line.Trim();
            var (key, value) = SplitKeyValue(trimmed);
            if (key is null) continue;

            if (indent == 0)
            {
                Flush();
                inInstances = false;

                switch (key)
                {
                    case "current":
                        current = value;
                        break;
                    case "instances":
                        inInstances = true;
                        break;
                    case "api_key" or "apiKey" or "token":
                        // Refused rather than ignored. This file is meant to be committed next to the
                        // config it describes; a schema that accepts a credential is one that will
                        // eventually receive one, and a leaked Full-scope key is a full admin
                        // credential (§4.6).
                        throw new CliException(
                            $"{path}: '{key}' is not allowed here — this file is meant to be committed. "
                            + "Set PIRO_API_KEY in the environment instead.");
                }
                continue;
            }

            if (!inInstances) continue;

            // Two indent levels below `instances:`: the name, then its fields.
            if (indent <= 2)
            {
                Flush();
                instanceName = key;
                continue;
            }

            switch (key)
            {
                case "url": url = value; break;
                case "config": config = value; break;
                case "admin_url": adminUrl = value; break;
                case "api_key" or "apiKey" or "token":
                    throw new CliException(
                        $"{path}: '{key}' is not allowed here — this file is meant to be committed. "
                        + "Set PIRO_API_KEY in the environment instead.");
            }
        }

        Flush();
        return new CliConfig(current, instances, path);
    }

    /// <summary>Drops a trailing comment, honouring quotes so a '#' inside a value survives.</summary>
    private static string StripComment(string line)
    {
        var inSingle = false;
        var inDouble = false;

        for (var i = 0; i < line.Length; i++)
        {
            switch (line[i])
            {
                case '\'' when !inDouble: inSingle = !inSingle; break;
                case '"' when !inSingle: inDouble = !inDouble; break;
                case '#' when !inSingle && !inDouble && (i == 0 || char.IsWhiteSpace(line[i - 1])):
                    return line[..i];
            }
        }

        return line;
    }

    private static (string? Key, string? Value) SplitKeyValue(string trimmed)
    {
        var colon = trimmed.IndexOf(':');
        if (colon < 0) return (null, null);

        var key = trimmed[..colon].Trim();
        var value = trimmed[(colon + 1)..].Trim();
        return (key, Unquote(value));
    }

    private static string? Unquote(string value)
    {
        if (value.Length == 0) return null;
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];
        return value;
    }
}
