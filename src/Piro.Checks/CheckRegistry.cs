using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>
/// Compile-time registry of every check the build ships, discovered from DI. Keyed by the check's stable
/// <see cref="ICheck.CheckId"/> (the value persisted on each Check row), so lookup never depends on a
/// closed enum. The same registry backs the in-process worker and a remote worker — both run this exact
/// set of checks.
/// </summary>
public sealed class CheckRegistry : ICheckRegistry
{
    private readonly Dictionary<string, ICheck> _byId;

    public CheckRegistry(IEnumerable<ICheck> checks)
    {
        _byId = checks.ToDictionary(c => c.CheckId, StringComparer.OrdinalIgnoreCase);
        All = [.. _byId.Values];
    }

    public IReadOnlyList<ICheck> All { get; }

    public ICheck? Find(string checkId) =>
        checkId is not null && _byId.TryGetValue(checkId, out var check) ? check : null;
}
