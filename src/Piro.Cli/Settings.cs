namespace Piro.Cli;

/// <summary>Where to talk to, as what, and which files to send.</summary>
internal sealed record Settings(string Url, string ApiKey, string InstanceName, string? ConfigTarget);

/// <summary>
/// Resolves connection settings from flags, environment and config file (RFC 0019 §4.6).
/// </summary>
/// <remarks>
/// Precedence, highest first: command-line flags, then <c>PIRO_URL</c> / <c>PIRO_API_KEY</c> /
/// <c>PIRO_INSTANCE</c>, then the nearest <c>piro.config.yml</c>, then <c>~/.piro/config.yml</c>.
/// Environment outranks the file so CI needs no file edits, and the credential never comes from the
/// file at all — that is enforced by the parser, which rejects the field outright.
/// </remarks>
internal static class SettingsResolver
{
    public static Settings Resolve(Options options, string workingDirectory)
    {
        var config = CliConfigLoader.Find(workingDirectory);

        var instanceName =
            options.Instance
            ?? Environment.GetEnvironmentVariable("PIRO_INSTANCE")
            ?? config?.Current;

        CliInstance? instance = null;
        if (config is not null && instanceName is not null)
        {
            if (!config.Instances.TryGetValue(instanceName, out instance))
                throw new CliException(
                    $"Instance '{instanceName}' is not defined in {config.Path}. "
                    + $"Known instances: {Known(config)}.");
        }

        var url =
            options.Url
            ?? Environment.GetEnvironmentVariable("PIRO_URL")
            ?? instance?.Url
            ?? throw new CliException(
                "No Piro URL configured. Pass --url, set PIRO_URL, or add one to "
                + $"{CliConfigLoader.FileName}.");

        var apiKey =
            Environment.GetEnvironmentVariable("PIRO_API_KEY")
            ?? throw new CliException(
                "No API key found. Set PIRO_API_KEY to a Full-scope key, which you can create under "
                + "Configuration → API keys in the admin panel.");

        return new Settings(
            url.TrimEnd('/'),
            apiKey,
            instanceName ?? "default",
            options.File ?? instance?.Config);
    }

    private static string Known(CliConfig config) =>
        config.Instances.Count == 0 ? "(none)" : string.Join(", ", config.Instances.Keys.Order());
}
