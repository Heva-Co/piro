using System.Text.Json;

namespace Piro.Cli;

/// <summary>The four commands of the API-key phase (RFC 0019 §4.6, phase 3).</summary>
internal static class Commands
{
    public static async Task<int> PlanAsync(Options options, CancellationToken ct)
    {
        var (settings, documents, client) = await PrepareAsync(options, ct);
        using (client)
        {
            var plan = await client.PlanAsync(new ConfigApplyRequest(documents, options.Prune), ct);
            return Report(plan, options, pendingIsFailure: true);
        }
    }

    public static async Task<int> ApplyAsync(Options options, CancellationToken ct)
    {
        var (settings, documents, client) = await PrepareAsync(options, ct);
        using (client)
        {
            // Always plan first: an apply must show what it will do before it does it, and the
            // confirmation is meaningless without the deletions listed by slug (§4.5).
            var plan = await client.PlanAsync(new ConfigApplyRequest(documents, options.Prune), ct);

            if (plan.Errors.Count > 0)
            {
                PlanRenderer.WriteErrors(plan.Errors, Console.Error);
                return ExitCode.Error;
            }

            if (!options.Json) PlanRenderer.WritePlan(plan, Console.Out);

            if (plan.Changes.All(c => c.Action == ConfigChangeAction.NoOp))
                return ExitCode.Success;

            if (!options.AutoApprove && !Confirm(plan, options))
            {
                Console.WriteLine("Cancelled. Nothing was applied.");
                return ExitCode.Error;
            }

            var applied = await client.ApplyAsync(new ConfigApplyRequest(documents, options.Prune), ct);
            var code = Report(applied, options, pendingIsFailure: false);

            // A write that landed but could not be scheduled is not a success — the checks exist and
            // are not running (§8).
            return applied.SchedulingErrors.Count > 0 ? ExitCode.Error : code;
        }
    }

    public static async Task<int> ExportAsync(Options options, CancellationToken ct)
    {
        var settings = await SettingsResolver.ResolveAsync(options, Directory.GetCurrentDirectory(), ct);
        using var client = new PiroApiClient(settings);

        await WriteTargetAsync(settings, client, ct);

        var yaml = await client.ExportAsync(ct);

        if (options.Output is null)
        {
            Console.Out.Write(yaml);
            return ExitCode.Success;
        }

        if (options.Split) return WriteSplit(yaml, options);

        var path = Path.GetFullPath(options.Output);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, yaml, ct);
        Console.Error.WriteLine($"Wrote {options.Output}");
        return ExitCode.Success;
    }

    /// <summary>
    /// Splits an exported document into one file per service — the layout most teams keep, and the
    /// one a CODEOWNERS rule can target per service (§4.8).
    /// </summary>
    private static int WriteSplit(string yaml, Options options)
    {
        var directory = Path.GetFullPath(options.Output!);

        // Overwriting a hand-maintained directory would discard comments and ordering, so it takes
        // an explicit --force.
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()
            && !options.Force)
            throw new CliException(
                $"{options.Output} is not empty. Pass --force to overwrite it.");

        Directory.CreateDirectory(directory);

        var written = 0;
        foreach (var (slug, content) in YamlSplitter.SplitByService(yaml))
        {
            File.WriteAllText(Path.Combine(directory, $"{slug}.yaml"), content);
            written++;
        }

        Console.Error.WriteLine($"Wrote {written} file(s) to {options.Output}");
        return ExitCode.Success;
    }

    public static int Init(Options options)
    {
        var directory = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(directory, CliConfigLoader.FileName);
        var examplePath = Path.Combine(directory, "piro.yaml");

        if (File.Exists(configPath) && !options.Force)
            throw new CliException($"{CliConfigLoader.FileName} already exists. Pass --force to overwrite.");

        File.WriteAllText(configPath, Scaffolding.ConfigFile);
        Console.Error.WriteLine($"Wrote {CliConfigLoader.FileName}");

        if (!File.Exists(examplePath))
        {
            File.WriteAllText(examplePath, Scaffolding.ExampleDocument);
            Console.Error.WriteLine("Wrote piro.yaml");
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("Next: set PIRO_API_KEY to a Full-scope key, then run `piro plan`.");
        return ExitCode.Success;
    }


    // ── Login ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Signs in through the browser (RFC 0019 §4.6). A browser rather than a password prompt because
    /// an OIDC or SAML-only instance has no password to collect, and because the CLI should never be
    /// in the credential-handling business.
    /// </summary>
    public static async Task<int> LoginAsync(Options options, CancellationToken ct)
    {
        var (url, instanceName, adminUrl) =
            SettingsResolver.ResolveTargetWithAdmin(options, Directory.GetCurrentDirectory());

        using var flow = new LoginFlow();
        flow.Start();

        var label = $"piro-cli on {MachineLabel()}";
        var consentUrl = flow.ConsentUrl(adminUrl, label);

        Console.Error.WriteLine($"Signing in to {url} ({instanceName})");
        Console.Error.WriteLine();

        // Printed whether or not the browser opens: this is what makes the flow usable over SSH,
        // where the CLI has no browser to launch.
        if (!LoginFlow.TryOpenBrowser(consentUrl))
            Console.Error.WriteLine("Could not open a browser. Open this URL to continue:");
        else
            Console.Error.WriteLine("Opened your browser. If nothing happened, open this URL:");

        Console.Error.WriteLine();
        Console.Error.WriteLine($"  {consentUrl}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Waiting for authorization…");

        var callback = await flow.WaitForCallbackAsync(TimeSpan.FromMinutes(5), ct);

        if (callback.Error is { } error)
            throw new CliException($"Authorization was denied: {error}");

        // Verified before the code is used: a loopback listener is reachable by any local process, so
        // a callback the CLI did not initiate must be refused outright.
        if (!string.Equals(callback.State, flow.State, StringComparison.Ordinal))
            throw new CliException(
                "The browser returned a response for a different sign-in attempt. Nothing was saved.");

        if (callback.Code is not { Length: > 0 } code)
            throw new CliException("The browser did not return an authorization code.");

        using var anonymous = PiroApiClient.Anonymous(url);
        var session = await anonymous.ExchangeCodeAsync(
            new CliTokenBody(code, flow.CodeVerifier, flow.RedirectUri), ct);

        CredentialStore.Save(new StoredCredential(url, session.RefreshToken, session.User?.Email));

        Console.Error.WriteLine();
        Console.Error.WriteLine(session.User?.Email is { } email
            ? $"Signed in as {email}."
            : "Signed in.");
        Console.Error.WriteLine("This session appears in your sessions list and can be revoked there.");
        return ExitCode.Success;
    }

    /// <summary>
    /// Revokes the stored session server-side, not just locally — a local delete would leave a live
    /// refresh token behind. Only this device's session ends; the browser session is untouched.
    /// </summary>
    public static async Task<int> LogoutAsync(Options options, CancellationToken ct)
    {
        var (url, _) = SettingsResolver.ResolveTarget(options, Directory.GetCurrentDirectory());

        if (CredentialStore.Find(url) is not { } stored)
        {
            Console.Error.WriteLine($"Not signed in to {url}.");
            return ExitCode.Success;
        }

        using var anonymous = PiroApiClient.Anonymous(url);
        var revoked = await anonymous.RefreshAsync(stored.RefreshToken, ct) is { } session
                      && await Revoke(url, session, ct);

        // The local copy goes regardless: leaving it would keep offering a credential the user has
        // already asked to be rid of.
        CredentialStore.Remove(url);

        Console.Error.WriteLine(revoked
            ? $"Signed out of {url}."
            : $"Removed the local session for {url}, but the server could not be reached to revoke it. "
              + "Revoke it from your sessions list.");

        return ExitCode.Success;
    }

    /// <summary>
    /// A name for this machine that a person will recognise in their sessions list, since that label
    /// is how they decide which session to revoke.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Environment.MachineName"/>: it truncates at the first dot, so a Mac whose host
    /// name comes from DHCP as "192.168.1.26" is labelled "192", which identifies nothing. On macOS
    /// the name the user actually chose lives in LocalHostName, which .NET does not expose, so it is
    /// read from the system. Everywhere else the DNS host name is already the right answer, and an
    /// address is kept whole rather than chopped at a dot.
    /// </remarks>
    private static string MachineLabel()
    {
        if (OperatingSystem.IsMacOS() && ReadMacComputerName() is { } macName) return macName;

        string? host = null;
        try { host = System.Net.Dns.GetHostName(); }
        catch (System.Net.Sockets.SocketException) { /* fall through */ }

        if (string.IsNullOrWhiteSpace(host))
            return string.IsNullOrWhiteSpace(Environment.MachineName) ? "unknown" : Environment.MachineName;

        if (System.Net.IPAddress.TryParse(host, out _)) return host;

        var firstLabel = host.Split('.')[0];
        return string.IsNullOrWhiteSpace(firstLabel) ? host : firstLabel;
    }

    /// <summary>Reads macOS's LocalHostName, or null if it cannot be determined.</summary>
    private static string? ReadMacComputerName()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/sbin/scutil",
                ArgumentList = { "--get", "LocalHostName" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null) return null;

            var name = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2000);

            return process.ExitCode == 0 && name.Length > 0 ? name : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<bool> Revoke(string url, SignInResponse session, CancellationToken ct)
    {
        using var client = new PiroApiClient(
            new Settings(url, new Credential(CredentialKind.AccessToken, session.AccessToken), "default", null));
        return await client.SignOutAsync(session.RefreshToken, ct);
    }

    // ── Shared ──────────────────────────────────────────────────────────────

    private static async Task<(Settings, IReadOnlyList<ConfigDocumentSource>, PiroApiClient)> PrepareAsync(
        Options options, CancellationToken ct)
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var settings = await SettingsResolver.ResolveAsync(options, workingDirectory, ct);

        var target = settings.ConfigTarget
            ?? (File.Exists(Path.Combine(workingDirectory, "piro.yaml")) ? "piro.yaml" : null)
            ?? throw new CliException(
                "No configuration files given. Pass -f, or set `config:` for this instance in "
                + $"{CliConfigLoader.FileName}.");

        var files = DocumentLoader.Resolve(target, options.Recursive);

        // Refusing an empty set matters most with --prune, where it would otherwise read as "delete
        // everything" — the exact shape of a too-narrow glob or a missing --recursive (§4.6).
        if (files.Count == 0)
            throw new CliException($"No YAML files found in {target}.");

        var client = new PiroApiClient(settings);
        await WriteTargetAsync(settings, client, ct);

        // Always print what was read, before anything else. A silently smaller file set is invisible
        // exactly when it is most destructive.
        Console.Error.WriteLine($"Reading {files.Count} file(s):");
        foreach (var file in files)
            Console.Error.WriteLine($"  {Path.GetRelativePath(workingDirectory, file)}");

        return (settings, DocumentLoader.Read(files, workingDirectory), client);
    }

    /// <summary>Names the instance and identity, so staging config never reaches production unseen.</summary>
    private static async Task WriteTargetAsync(Settings settings, PiroApiClient client, CancellationToken ct)
    {
        var user = await client.WhoAmIAsync(ct);
        var identity = user?.Email ?? user?.Name;

        Console.Error.WriteLine(identity is null
            ? $"Target: {settings.Url} ({settings.InstanceName})"
            : $"Target: {settings.Url} ({settings.InstanceName}) as {identity}");
        Console.Error.WriteLine();
    }

    private static int Report(ConfigPlanDto plan, Options options, bool pendingIsFailure)
    {
        if (plan.Errors.Count > 0)
        {
            if (options.Json) WriteJson(plan);
            else PlanRenderer.WriteErrors(plan.Errors, Console.Error);
            return ExitCode.Error;
        }

        if (options.Json) WriteJson(plan);
        else PlanRenderer.WritePlan(plan, Console.Out);

        var pending = plan.Changes.Any(c => c.Action != ConfigChangeAction.NoOp);
        return pendingIsFailure && pending ? ExitCode.ChangesPending : ExitCode.Success;
    }

    private static void WriteJson(ConfigPlanDto plan) =>
        Console.Out.WriteLine(JsonSerializer.Serialize(
            plan, CliJsonContext.Default.ConfigPlanDto));

    /// <summary>
    /// Asks before writing. A plain apply takes any affirmative; a prune requires typing the word,
    /// because whoever runs it is asserting the files are the complete truth for the instance (§4.5).
    /// </summary>
    private static bool Confirm(ConfigPlanDto plan, Options options)
    {
        var deletions = plan.Changes.Count(c => c.Action == ConfigChangeAction.Delete);

        if (Console.IsInputRedirected)
            throw new CliException(
                "Cannot prompt for confirmation because input is not a terminal. "
                + "Pass --auto-approve to apply non-interactively.");

        Console.WriteLine();
        if (options.Prune && deletions > 0)
        {
            Console.Write($"This deletes {deletions} resource(s), including their history. Type 'delete' to confirm: ");
            return Console.ReadLine()?.Trim() == "delete";
        }

        Console.Write("Apply these changes? [y/N] ");
        var answer = Console.ReadLine()?.Trim();
        return answer is "y" or "Y" or "yes" or "Yes";
    }
}
