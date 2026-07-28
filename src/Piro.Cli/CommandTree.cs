using System.CommandLine;

namespace Piro.Cli;

/// <summary>
/// The command surface, built with <c>System.CommandLine</c> — Microsoft's parser, which gives help,
/// parse errors, suggestions and shell completions without hand-rolling them, and is designed for
/// NativeAOT (its binding is explicit, not reflection-driven).
/// </summary>
internal static class CommandTree
{
    // Shared across commands, so a flag means the same thing everywhere it appears.
    private static readonly Option<string?> FileOption =
        new("--file", "-f") { Description = "A file, a directory, or a glob (default: from piro.config.yml)." };

    private static readonly Option<string?> UrlOption =
        new("--url") { Description = "Piro base URL (default: PIRO_URL, then piro.config.yml)." };

    private static readonly Option<string?> InstanceOption =
        new("--instance") { Description = "Which configured instance to use." };

    private static readonly Option<bool> RecursiveOption =
        new("--recursive", "-r") { Description = "Descend into subdirectories when -f is a directory." };

    private static readonly Option<bool> PruneOption =
        new("--prune") { Description = "Delete resources the files do not declare." };

    private static readonly Option<bool> AutoApproveOption =
        new("--auto-approve") { Description = "Skip the confirmation prompt." };

    private static readonly Option<bool> JsonOption =
        new("--json") { Description = "Print the plan as JSON." };

    private static readonly Option<string?> OutputOption =
        new("--output", "-o") { Description = "Where to write (default: stdout)." };

    private static readonly Option<bool> SplitOption =
        new("--split") { Description = "One file per service, into the -o directory." };

    private static readonly Option<bool> ForceOption =
        new("--force") { Description = "Overwrite existing files." };

    public static RootCommand Build()
    {
        var root = new RootCommand("Manage Piro monitoring configuration as code.")
        {
            Plan(),
            Apply(),
            Export(),
            Init(),
        };

        // Exit codes are a contract so CI can branch without parsing output (RFC 0019 §4.6):
        //   0 success (for plan, no changes)   1 error   2 plan only, changes pending
        root.Description += """


            Exit codes:
              0  success; for plan, no changes are pending
              1  error
              2  plan only: changes are pending

            Environment:
              PIRO_URL       Base URL of the instance
              PIRO_API_KEY   A Full-scope API key (required; never read from a file)
              PIRO_INSTANCE  Which configured instance to use
            """;

        return root;
    }

    private static Command Plan()
    {
        var command = new Command("plan", "Show what would change. Writes nothing.")
        {
            FileOption, UrlOption, InstanceOption, RecursiveOption, PruneOption, JsonOption,
        };

        command.SetAction((result, ct) => Commands.PlanAsync(Read(result), ct));
        return command;
    }

    private static Command Apply()
    {
        var command = new Command("apply", "Apply the configuration.")
        {
            FileOption, UrlOption, InstanceOption, RecursiveOption, PruneOption, AutoApproveOption, JsonOption,
        };

        command.SetAction((result, ct) => Commands.ApplyAsync(Read(result), ct));
        return command;
    }

    private static Command Export()
    {
        var command = new Command("export", "Write the current instance as piro.yaml.")
        {
            OutputOption, SplitOption, ForceOption, UrlOption, InstanceOption,
        };

        command.SetAction((result, ct) => Commands.ExportAsync(Read(result), ct));
        return command;
    }

    private static Command Init()
    {
        var command = new Command("init", "Scaffold piro.config.yml and an example piro.yaml.")
        {
            ForceOption,
        };

        command.SetAction(result => Commands.Init(Read(result)));
        return command;
    }

    /// <summary>
    /// Collects parsed values into one record, so the command bodies take plain data and stay
    /// testable without a ParseResult.
    /// </summary>
    private static Options Read(ParseResult result) => new()
    {
        File = result.GetValue(FileOption),
        Output = result.GetValue(OutputOption),
        Url = result.GetValue(UrlOption),
        Instance = result.GetValue(InstanceOption),
        Prune = result.GetValue(PruneOption),
        AutoApprove = result.GetValue(AutoApproveOption),
        Recursive = result.GetValue(RecursiveOption),
        Split = result.GetValue(SplitOption),
        Force = result.GetValue(ForceOption),
        Json = result.GetValue(JsonOption),
    };
}
