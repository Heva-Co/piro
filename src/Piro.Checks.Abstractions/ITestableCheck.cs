namespace Piro.Checks.Abstractions;

/// <summary>
/// An optional capability a check may implement to support an on-demand "Test" run that captures
/// diagnostic output and returns it to the caller without persisting a datapoint or evaluating alerts
/// (RFC 0010 §4.6). Kept off the core <see cref="ICheck"/> contract because only some checks have
/// anything to test interactively (today: the Script check, whose <c>console.log</c> is captured here);
/// the app layer resolves the check from the registry and tests it only when it is an
/// <see cref="ITestableCheck"/>. The test path runs the exact same probe logic as production — the only
/// difference is that diagnostics are captured rather than discarded, so there is no "worked in test,
/// failed live" gap.
/// </summary>
public interface ITestableCheck
{
    /// <summary>
    /// Runs the probe against <paramref name="config"/> (deserialized to <see cref="CheckManifest.ConfigType"/>)
    /// in debug mode, capturing any diagnostic lines into <paramref name="logs"/>. Returns the raw result;
    /// the caller does not persist it. Must not throw for an expected script failure — a broken script is
    /// a <see cref="CheckOutcome.Error"/> result, like any other probe error.
    /// </summary>
    CheckProbeResult ProbeForTest(object config, ICheckHost host, out IReadOnlyList<string> logs);
}
