namespace Piro.Cli;

/// <summary>
/// Turns a <c>-f</c> target — a file, a directory, or a glob — into the documents to send
/// (RFC 0019 §4.6).
/// </summary>
/// <remarks>
/// This is where a mistake becomes destructive rather than merely annoying. With a single file a
/// wrong path fails loudly. With a directory, a too-narrow glob, a missing <c>--recursive</c>, or a
/// stray <c>.yml.bak</c> silently yields a <em>smaller</em> document set, and <c>--prune</c> then
/// deletes everything the unread files declared. So resolution is always reported, and an empty
/// result is an error rather than an empty document.
/// </remarks>
internal static class DocumentLoader
{
    private static readonly string[] Extensions = [".yaml", ".yml"];

    /// <summary>
    /// Resolves the target to a sorted list of files. Lexicographic order by full path keeps the
    /// result identical across machines and CI runners.
    /// </summary>
    public static IReadOnlyList<string> Resolve(string target, bool recursive)
    {
        var expanded = Path.GetFullPath(target);

        if (File.Exists(expanded)) return [expanded];

        if (Directory.Exists(expanded))
        {
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return [.. Directory
                .EnumerateFiles(expanded, "*", option)
                .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal)];
        }

        // Neither a file nor a directory: treat it as a glob against its parent.
        var directory = Path.GetDirectoryName(expanded);
        var pattern = Path.GetFileName(expanded);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory) || string.IsNullOrEmpty(pattern))
            throw new CliException($"No such file or directory: {target}");

        var option2 = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var matches = Directory
            .EnumerateFiles(directory, pattern, option2)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (matches.Count == 0)
            throw new CliException($"No files matched: {target}");

        return matches;
    }

    /// <summary>
    /// Reads the resolved files, tagging each with a path relative to the working directory so an
    /// error names the file the user knows rather than an absolute path from a CI runner.
    /// </summary>
    public static IReadOnlyList<ConfigDocumentSource> Read(
        IReadOnlyList<string> files, string workingDirectory)
    {
        return [.. files.Select(file => new ConfigDocumentSource(
            Relative(file, workingDirectory),
            File.ReadAllText(file)))];
    }

    private static string Relative(string file, string workingDirectory)
    {
        var relative = Path.GetRelativePath(workingDirectory, file);
        // A path that climbs out of the working directory reads worse than the absolute one.
        return relative.StartsWith("..", StringComparison.Ordinal) ? file : relative;
    }
}
