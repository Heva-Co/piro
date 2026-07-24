using System.Text.Json.Serialization;
using Piro.Contracts;

namespace Piro.Checks;

/// <summary>
/// Configuration for a push-free, sandboxed-JavaScript <c>Script</c> check (RFC 0010). The operator
/// writes a small ES module exporting a parameterless <c>check()</c> that drives its own HTTP via the
/// <c>piro:http</c> module and returns a raw verdict <c>{ up, message?, dimensions? }</c>. There is no
/// URL/method/headers here — the script issues its own requests. Round-tripped through the check's
/// existing <c>TypeDataJson</c> column; no new schema.
/// </summary>
public record ScriptCheckConfig
{
    /// <summary>
    /// The ES module: <c>import http from 'piro:http'; export function check() { … }</c>. Rendered in a
    /// code editor (<see cref="CodeFieldAttribute"/> → <c>ConfigFieldType.Code</c>).
    /// </summary>
    [ConfigField("Script", HelpText = "A JavaScript module exporting check() -> { up, message?, dimensions? }. Use the piro:http module for requests.")]
    [CodeField]
    public string Script { get; init; } = DefaultTemplate;

    /// <summary>Starter shown when creating a new Script check, so the operator edits a working example.</summary>
    internal const string DefaultTemplate =
        """
        import http from 'piro:http';

        export function check() {
          const r = http.get('https://example.com/health');
          // Return { up } for a simple up/down check. Add a numeric `dimensions` bag
          // (e.g. { Severity: 1 }) and threshold it in an alert config for DEGRADED.
          return { up: r.statusCode === 200, message: 'HTTP ' + r.statusCode };
        }
        """;

    /// <summary>
    /// Whole-script wall-clock budget in milliseconds — all of <c>check()</c>'s logic plus every
    /// <c>http.get</c> it makes. Overrunning it aborts the run and reports a check <c>Error</c>. Accepts
    /// both "timeout" and "timeoutMs" from JSON.
    /// </summary>
    [ConfigField("Timeout (ms)", HelpText = "Whole-script wall-clock budget. Must be shorter than the check interval.")]
    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 10_000;

    /// <summary>Cap on any single <c>http.get</c> response body, in bytes (default 1 MiB). Bounds memory.</summary>
    [ConfigField("Max response bytes", HelpText = "Cap on any http.get response body (default 1 MiB).")]
    public int MaxResponseBytes { get; init; } = 1_048_576;
}
