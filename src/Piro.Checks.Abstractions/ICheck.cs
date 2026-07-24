namespace Piro.Checks.Abstractions;

/// <summary>
/// A self-describing check type (RFC 0016-style). Authors implement the strongly-typed
/// <see cref="ICheck{TConfig}"/> (or derive from <see cref="Check{TConfig}"/>); this non-generic base is
/// what the registry stores and what Piro calls, so a heterogeneous set of checks lives in one list.
/// A check receives only its config plus an <see cref="ICheckHost"/> — never Piro's <c>Check</c> entity,
/// a repository, or how the result becomes a status.
/// </summary>
public interface ICheck
{
    /// <summary>
    /// Stable, permanent identifier — the discriminator persisted on every <c>Check</c> row. Equals the
    /// current CheckType member name verbatim ("HTTP", "DNS", "GCP_CloudRunJob") so no stored data has to
    /// migrate. Immutable once shipped.
    /// </summary>
    string CheckId { get; }

    /// <summary>Everything Piro knows about this check type — see <see cref="CheckManifest"/>.</summary>
    CheckManifest Manifest { get; }

    /// <summary>
    /// Runs one probe against a config already deserialized into <see cref="CheckManifest.ConfigType"/>.
    /// The typed override (<see cref="ICheck{TConfig}.ProbeAsync"/>) is the author-facing entry point;
    /// this non-generic method is Piro's call site. Returns a raw <see cref="CheckProbeResult"/> — the
    /// check must not decide severity and must not throw for an expected failure.
    /// </summary>
    Task<CheckProbeResult> ProbeAsync(object config, ICheckHost host, CancellationToken ct = default);
}

/// <summary>
/// The strongly-typed check contract an author implements: it works directly with its own
/// <typeparamref name="TConfig"/>, no casting. Pair it with <see cref="Check{TConfig}"/> for the
/// non-generic bridge, or implement both explicitly.
/// </summary>
public interface ICheck<in TConfig> : ICheck where TConfig : class
{
    /// <summary>Runs one probe against this check's own typed config.</summary>
    Task<CheckProbeResult> ProbeAsync(TConfig config, ICheckHost host, CancellationToken ct = default);
}

/// <summary>
/// Base class for a typed check: implement <see cref="ProbeAsync(TConfig, ICheckHost, CancellationToken)"/>
/// and this bridges the non-generic <see cref="ICheck.ProbeAsync(object, ICheckHost, CancellationToken)"/>
/// with a single cast at the boundary (Piro always passes a config of <c>Manifest.ConfigType</c>).
/// </summary>
public abstract class Check<TConfig> : ICheck<TConfig> where TConfig : class
{
    public abstract string CheckId { get; }
    public abstract CheckManifest Manifest { get; }

    public abstract Task<CheckProbeResult> ProbeAsync(TConfig config, ICheckHost host, CancellationToken ct = default);

    Task<CheckProbeResult> ICheck.ProbeAsync(object config, ICheckHost host, CancellationToken ct) =>
        ProbeAsync(
            config as TConfig
                ?? throw new InvalidOperationException(
                    $"Check '{CheckId}' expected config of type {typeof(TConfig).Name} but got {config?.GetType().Name ?? "null"}."),
            host, ct);
}
