using System.CommandLine;
using Piro.Cli;

// System.CommandLine owns parsing, help and cancellation. Everything below is error presentation:
// a CliException is a message for the user, anything else is a bug and says so.
var configuration = new InvocationConfiguration
{
    EnableDefaultExceptionHandler = false,
};

try
{
    return await CommandTree.Build().Parse(args).InvokeAsync(configuration);
}
catch (CliException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return ExitCode.Error;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return ExitCode.Error;
}

internal static class ExitCode
{
    public const int Success = 0;
    public const int Error = 1;

    /// <summary>Plan only: the instance differs from the files. Lets CI gate a pull request.</summary>
    public const int ChangesPending = 2;
}
