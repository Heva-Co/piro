using System.Diagnostics;
using System.Net.Http;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>
/// Runs a small sandboxed-JavaScript check (RFC 0010). The operator writes an ES module that exports a
/// parameterless <c>check()</c>, drives its own HTTP through the <c>piro:http</c> module, and returns a
/// raw verdict <c>{ up, message?, dimensions? }</c>. The check reports raw state only — <c>up</c> maps to
/// Up/Down and any numeric <c>dimensions</c> are attached for the alert policy to threshold; severity
/// (including DEGRADED) is never the script's call. A fresh Jint engine is built per probe (Jint is not
/// thread-safe and Piro runs checks concurrently), deny-by-default: no CLR interop, no filesystem, no
/// timers, and only the <c>piro:http</c> module resolves.
/// </summary>
public sealed class ScriptCheck : Check<ScriptCheckConfig>, ITestableCheck
{
    // Jint's whole-script wall-clock (TimeoutInterval) is authoritative; these bound CPU and memory.
    private const int MaxStatements = 5_000_000;
    private const long MaxMemoryBytes = 24 * 1024 * 1024;

    public override string CheckId => "Script";

    public override CheckManifest Manifest => new()
    {
        Label = "Script",
        Description = "Run a small sandboxed JavaScript check() that drives its own HTTP and returns a verdict.",
        ConfigType = typeof(ScriptCheckConfig),
        // Status + Latency always; a script may also emit its own numeric dimensions (declared per-script,
        // surfaced through the result) that the policy can threshold — e.g. a "Severity" score.
        Dimensions = [CommonDimensions.Status, CommonDimensions.Latency],
        DefaultIntervalSeconds = 300, // a script runs arbitrary code, so its floor is more conservative
    };

    public override Task<CheckProbeResult> ProbeAsync(ScriptCheckConfig config, ICheckHost host, CancellationToken ct = default) =>
        Task.FromResult(Run(config, host, captureLogs: null));

    /// <summary>
    /// Debug entry point for the on-demand "Test" run (RFC 0010 §4.6): identical to a production probe,
    /// but <c>console.log</c> lines are captured into <paramref name="logs"/> instead of discarded, and
    /// the result is returned to the caller rather than persisted. Kept off the <see cref="ICheck"/>
    /// contract — only the script check has logs, and only the test endpoint calls this — so the SDK
    /// stays pure for every other check. `http.get` is real here too, so Test exercises the exact code
    /// path production runs.
    /// </summary>
    public CheckProbeResult ProbeForTest(object config, ICheckHost host, out IReadOnlyList<string> logs)
    {
        var buffer = new List<string>();
        var typed = config as ScriptCheckConfig
            ?? throw new InvalidOperationException($"Script test expected {nameof(ScriptCheckConfig)} but got {config?.GetType().Name ?? "null"}.");
        var result = Run(typed, host, captureLogs: buffer.Add);
        logs = buffer;
        return result;
    }

    // The single execution path both modes share. captureLogs is null in production (console.log is a
    // no-op) and a sink in debug — the ONLY difference between the two, so there is no "worked in test,
    // failed in prod" gap.
    private CheckProbeResult Run(ScriptCheckConfig config, ICheckHost host, Action<string>? captureLogs)
    {
        if (string.IsNullOrWhiteSpace(config.Script))
            return CheckProbeResult.Failed("Script is empty.");

        var client = host.GetRequiredService<IHttpClientFactory>().CreateClient(ScriptHttpClientName);
        var http = new ScriptHttp(client, config.MaxResponseBytes);

        var engine = new Engine(o => o
            .TimeoutInterval(TimeSpan.FromMilliseconds(config.TimeoutMs)) // whole-script wall-clock kill
            .LimitMemory(MaxMemoryBytes)
            .MaxStatements(MaxStatements)
            .Strict());
        // Only piro:http resolves; any other import (node:fs, a URL, …) throws at module load. CLR interop
        // is left OFF (Jint default), so a script can never reach a System.* type or Piro internals.
        engine.Modules.Add("piro:http", b => b.ExportObject("default", http));
        engine.SetValue("console", new ScriptConsole(captureLogs));
        engine.Modules.Add(ScriptModuleSpecifier, b => b.AddSource(config.Script));

        var sw = Stopwatch.StartNew();
        try
        {
            var ns = engine.Modules.Import(ScriptModuleSpecifier);
            var check = ns.Get("check");
            if (!check.IsCallable())
                return CheckProbeResult.Failed("Script must export a function named 'check'.");

            var raw = engine.Invoke(check);
            sw.Stop();
            return MapReturn(raw, sw.Elapsed.TotalMilliseconds);
        }
        catch (ScriptEgressBlockedException ex)
        {
            return CheckProbeResult.Failed(ex.Message);
        }
        catch (TimeoutException)
        {
            return CheckProbeResult.Failed($"Script timed out after {config.TimeoutMs} ms.");
        }
        catch (JavaScriptException ex)
        {
            return CheckProbeResult.Failed($"Script error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CheckProbeResult.Failed($"Script could not run: {ex.Message}");
        }
    }

    /// <summary>
    /// The <c>console</c> global bound into the engine. <c>console.log(...)</c> joins its args into the
    /// capture sink in debug mode, or is a no-op in production (<paramref name="capture"/> is null) — the
    /// single behavioral difference between the two run modes. Jint marshals variadic JS args into the
    /// <c>params</c> array.
    /// </summary>
    private sealed class ScriptConsole(Action<string>? capture)
    {
        // Lowercased to read as console.log(...) from JS.
        public void log(params object?[] args) =>
            capture?.Invoke(string.Join(" ", args.Select(a => a?.ToString() ?? "null")));
    }

    /// <summary>
    /// Maps the script's <c>{ up, message?, dimensions? }</c> return to a raw <see cref="CheckProbeResult"/>.
    /// A non-object return, or one missing a boolean <c>up</c>, is an <c>Error</c> (a broken script, not an
    /// outage). Latency is always attached; each numeric entry in <c>dimensions</c> becomes a
    /// higher-is-worse threshold dimension the policy can alert on.
    /// </summary>
    internal static CheckProbeResult MapReturn(JsValue raw, double latencyMs)
    {
        if (!raw.IsObject())
            return CheckProbeResult.Failed("Script must return an object like { up: boolean, message?, dimensions? }.");

        var obj = raw.AsObject();
        var upValue = obj.Get("up");
        if (!upValue.IsBoolean())
            return CheckProbeResult.Failed("Script return is missing a boolean 'up'.");

        var message = obj.Get("message");
        var messageText = message.IsString() ? message.AsString() : null;

        var dimensions = new List<CheckDimension> { CommonDimensions.Latency.Measure(latencyMs) };
        var dimsValue = obj.Get("dimensions");
        if (dimsValue.IsObject())
        {
            var dims = dimsValue.AsObject();
            foreach (var key in dims.GetOwnPropertyKeys())
            {
                var name = key.ToString();
                var value = dims.Get(name);
                if (value.IsNumber())
                    dimensions.Add(new CheckDimension(name, value.AsNumber(), Piro.Contracts.ThresholdDirection.HigherIsWorse));
            }
        }

        return upValue.AsBoolean()
            ? CheckProbeResult.Ok([.. dimensions])
            : CheckProbeResult.DownWith(messageText ?? "Script reported the target as down.", [.. dimensions]);
    }

    /// <summary>
    /// Dedicated SSRF-guarded client for script egress (RFC 0010 §4.4), distinct from the shared
    /// "piro-http" the HTTP check uses — so the guard is added here without changing existing check
    /// behavior. Retrofitting the guard onto the other clients is a separate follow-up (RFC 0010 §6).
    /// </summary>
    internal const string ScriptHttpClientName = "piro-script-http";
    private const string ScriptModuleSpecifier = "piro:script";
}
