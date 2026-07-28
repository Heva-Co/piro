namespace Piro.Cli;

/// <summary>
/// The parsed command line, flattened into plain data so command bodies never touch a
/// <c>ParseResult</c> and stay directly testable.
/// </summary>
internal sealed record Options
{
    public string? File { get; init; }
    public string? Output { get; init; }
    public string? Url { get; init; }
    public string? Instance { get; init; }
    public bool Prune { get; init; }
    public bool AutoApprove { get; init; }
    public bool Recursive { get; init; }
    public bool Split { get; init; }
    public bool Force { get; init; }
    public bool Json { get; init; }
}
