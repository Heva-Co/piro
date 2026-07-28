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
        var settings = SettingsResolver.Resolve(options, Directory.GetCurrentDirectory());
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

    // ── Shared ──────────────────────────────────────────────────────────────

    private static async Task<(Settings, IReadOnlyList<ConfigDocumentSource>, PiroApiClient)> PrepareAsync(
        Options options, CancellationToken ct)
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var settings = SettingsResolver.Resolve(options, workingDirectory);

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
