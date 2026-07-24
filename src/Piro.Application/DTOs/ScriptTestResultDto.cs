namespace Piro.Application.DTOs;

/// <summary>
/// Result of an on-demand Script check "Test" run (RFC 0010 §4.6): the raw verdict the script produced
/// plus captured <c>console.log</c> output. Deliberately NOT a severity — the Test panel shows the raw
/// outcome; UP/DEGRADED/DOWN is the alert policy's decision once the check is saved with an AlertConfig.
/// Never persisted; returned in the HTTP response only.
/// </summary>
/// <param name="Outcome">The raw probe outcome: "Up", "Down", or "Error" (a broken script).</param>
/// <param name="Message">Human-readable detail for a Down/Error; null on a clean Up.</param>
/// <param name="LatencyMs">Whole-script wall-clock time.</param>
/// <param name="Dimensions">Numeric measurements the script emitted (e.g. "Severity"), by name.</param>
/// <param name="Logs">Captured console.log lines, in order.</param>
public record ScriptTestResultDto(
    string Outcome,
    string? Message,
    double? LatencyMs,
    IReadOnlyDictionary<string, double> Dimensions,
    IReadOnlyList<string> Logs);
