namespace Piro.Cli;

/// <summary>Which kind of credential the CLI is holding.</summary>
internal enum CredentialKind
{
    /// <summary>A Full-scope API key from <c>PIRO_API_KEY</c>, sent as <c>X-Api-Key</c>.</summary>
    ApiKey,

    /// <summary>A bearer token from a stored <c>piro login</c> session.</summary>
    AccessToken,
}

internal sealed record Credential(CredentialKind Kind, string Value);

/// <summary>Where to talk to, as what, and which files to send.</summary>
internal sealed record Settings(
    string Url, Credential Credential, string InstanceName, string? ConfigTarget);

/// <summary>
/// Resolves connection settings from flags, environment, config file and stored session
/// (RFC 0019 §4.6).
/// </summary>
/// <remarks>
/// Precedence, highest first: command-line flags, then <c>PIRO_URL</c> / <c>PIRO_API_KEY</c> /
/// <c>PIRO_INSTANCE</c>, then the nearest <c>piro.config.yml</c>, then <c>~/.piro/config.yml</c>, and
/// for credentials only, last of all, the session from <c>piro login</c>. Environment outranks the
/// file so CI needs no file edits, and the stored session comes last so an explicit PIRO_API_KEY in a
/// CI job is never shadowed by a developer's cached login.
/// </remarks>
internal static class SettingsResolver
{
    /// <summary>Resolves the URL without requiring a credential — what <c>login</c> needs.</summary>
    public static (string Url, string InstanceName) ResolveTarget(Options options, string workingDirectory)
    {
        var config = CliConfigLoader.Find(workingDirectory);
        var (instanceName, instance) = ResolveInstance(options, config);

        var url =
            options.Url
            ?? Environment.GetEnvironmentVariable("PIRO_URL")
            ?? instance?.Url
            ?? throw new CliException(
                "No Piro URL configured. Pass --url, set PIRO_URL, or add one to "
                + $"{CliConfigLoader.FileName}.");

        return (url.TrimEnd('/'), instanceName ?? "default");
    }

    public static async Task<Settings> ResolveAsync(
        Options options, string workingDirectory, CancellationToken ct)
    {
        var config = CliConfigLoader.Find(workingDirectory);
        var (_, instance) = ResolveInstance(options, config);
        var (url, resolvedName) = ResolveTarget(options, workingDirectory);

        var credential = await ResolveCredentialAsync(url, ct);

        return new Settings(url, credential, resolvedName, options.File ?? instance?.Config);
    }

    private static (string? Name, CliInstance? Instance) ResolveInstance(Options options, CliConfig? config)
    {
        var instanceName =
            options.Instance
            ?? Environment.GetEnvironmentVariable("PIRO_INSTANCE")
            ?? config?.Current;

        if (config is null || instanceName is null) return (instanceName, null);

        if (!config.Instances.TryGetValue(instanceName, out var instance))
            throw new CliException(
                $"Instance '{instanceName}' is not defined in {config.Path}. "
                + $"Known instances: {Known(config)}.");

        return (instanceName, instance);
    }

    private static async Task<Credential> ResolveCredentialAsync(string url, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("PIRO_API_KEY") is { Length: > 0 } apiKey)
            return new Credential(CredentialKind.ApiKey, apiKey);

        if (CredentialStore.Find(url) is not { } stored)
            throw new CliException(
                "Not authenticated. Run `piro login`, or set PIRO_API_KEY to a Full-scope key.");

        // Only the refresh token is stored; the short-lived access token is obtained per invocation.
        // Refresh also rotates the stored token, so a leaked credentials file ages out of usefulness.
        using var anonymous = PiroApiClient.Anonymous(url);
        var session = await anonymous.RefreshAsync(stored.RefreshToken, ct)
            ?? throw new CliException(
                "Your saved session has expired. Run `piro login` again.");

        CredentialStore.Save(stored with { RefreshToken = session.RefreshToken });
        return new Credential(CredentialKind.AccessToken, session.AccessToken);
    }

    private static string Known(CliConfig config) =>
        config.Instances.Count == 0 ? "(none)" : string.Join(", ", config.Instances.Keys.Order());
}
