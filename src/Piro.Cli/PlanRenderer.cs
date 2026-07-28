namespace Piro.Cli;

/// <summary>Renders a plan for a terminal, in the shape a reviewer reads top to bottom.</summary>
internal static class PlanRenderer
{
    /// <summary>
    /// Prints validation errors, each located in the file that caused it. A directory of twenty files
    /// has to produce messages a user can navigate, which is the whole reason paths survive to the
    /// server and back (RFC 0019 §4.6).
    /// </summary>
    public static void WriteErrors(IReadOnlyList<ConfigValidationError> errors, TextWriter output)
    {
        output.WriteLine();
        output.WriteLine(errors.Count == 1 ? "1 error:" : $"{errors.Count} errors:");
        output.WriteLine();

        foreach (var error in errors)
        {
            var location = Location(error);
            output.WriteLine(location is null ? $"  {error.Message}" : $"  {location}");
            if (location is not null) output.WriteLine($"      {error.Message}");
            if (error.Pointer is not null) output.WriteLine($"      at {error.Pointer}");
            output.WriteLine();
        }
    }

    private static string? Location(ConfigValidationError error)
    {
        if (error.Path is null) return null;
        if (error.Line is null) return error.Path;
        return error.Column is null
            ? $"{error.Path}:{error.Line}"
            : $"{error.Path}:{error.Line}:{error.Column}";
    }

    public static void WritePlan(ConfigPlanDto plan, TextWriter output)
    {
        var acting = plan.Changes.Where(c => c.Action != ConfigChangeAction.NoOp).ToList();

        output.WriteLine();
        if (acting.Count == 0)
        {
            output.WriteLine("No changes. The instance already matches the configuration.");
        }
        else
        {
            output.WriteLine(plan.Applied ? "Applied:" : "Planned changes:");
            output.WriteLine();
            foreach (var change in Ordered(acting))
                WriteChange(change, output);
        }

        WriteSummary(plan, output);

        // Untouched resources are the visible proof that a partial adoption did nothing to the rest.
        if (plan.Untouched.Count > 0)
        {
            output.WriteLine();
            output.WriteLine($"{plan.Untouched.Count} resource(s) exist in Piro but are not in these files, "
                + "and were left alone:");
            foreach (var slug in plan.Untouched.Take(10)) output.WriteLine($"  {slug}");
            if (plan.Untouched.Count > 10)
                output.WriteLine($"  … and {plan.Untouched.Count - 10} more");
            output.WriteLine();
            output.WriteLine("Pass --prune to delete them instead.");
        }

        if (plan.SchedulingErrors.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Saved, but the scheduler could not be updated:");
            foreach (var error in plan.SchedulingErrors) output.WriteLine($"  {error}");
        }
    }

    /// <summary>Deletions last, and grouped by parent, so the destructive part is read as a block.</summary>
    private static IEnumerable<ConfigResourceChange> Ordered(IEnumerable<ConfigResourceChange> changes) =>
        changes
            .OrderBy(c => c.Action == ConfigChangeAction.Delete ? 1 : 0)
            .ThenBy(c => c.ParentSlug ?? c.Slug, StringComparer.Ordinal)
            .ThenBy(c => c.Kind)
            .ThenBy(c => c.Slug, StringComparer.Ordinal);

    private static void WriteChange(ConfigResourceChange change, TextWriter output)
    {
        var name = change.ParentSlug is null ? change.Slug : $"{change.ParentSlug}/{change.Slug}";
        output.WriteLine($"  {Marker(change.Action)} {Kind(change.Kind)} {name}");

        foreach (var field in change.Fields ?? [])
            output.WriteLine($"      {field.Field}: {Value(field.Before)} → {Value(field.After)}");

        // Warnings carry the consequences a user must see before approving — above all that deleting
        // a check discards its measurement history (§8).
        foreach (var warning in change.Warnings ?? [])
            output.WriteLine($"      ! {warning}");
    }

    private static string Marker(ConfigChangeAction action) => action switch
    {
        ConfigChangeAction.Create => "+",
        ConfigChangeAction.Update => "~",
        ConfigChangeAction.Delete => "-",
        _ => " ",
    };

    private static string Kind(ConfigResourceKind kind) => kind switch
    {
        ConfigResourceKind.Service => "service",
        ConfigResourceKind.Check => "check",
        ConfigResourceKind.AlertConfig => "alert",
        _ => "resource",
    };

    private static string Value(string? value) => value is null ? "(unset)" : $"\"{value}\"";

    private static void WriteSummary(ConfigPlanDto plan, TextWriter output)
    {
        var s = plan.Summary;
        output.WriteLine();
        output.WriteLine(plan.Applied
            ? $"Applied: {s.Create} created, {s.Update} updated, {s.Delete} deleted, {s.NoOp} unchanged."
            : $"Plan: {s.Create} to create, {s.Update} to update, {s.Delete} to delete, {s.NoOp} unchanged.");
    }
}
