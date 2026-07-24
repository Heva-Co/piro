using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Piro.Checks;

/// <summary>
/// The <c>piro:http</c> module exposed to a Script check (RFC 0010 §4.3): the script's <em>only</em>
/// network egress. GET-only, SSRF-guarded (the backing client carries <see cref="ScriptSsrfGuard"/>),
/// and size-capped. Returned to the script as the module's default export, so
/// <c>import http from 'piro:http'; http.get(url)</c> works. Methods are named lowercase to read as
/// idiomatic JS. Synchronous by design — Jint invokes the script synchronously, so <c>get</c> blocks on
/// the request within the whole-script wall-clock budget the engine enforces.
/// </summary>
internal sealed class ScriptHttp(HttpClient client, int maxResponseBytes)
{
    // ReSharper disable once InconsistentNaming — exposed to JS as http.get(...)
    public ScriptHttpResponse get(string url) => GetInternal(url, null);

    // ReSharper disable once InconsistentNaming
    public ScriptHttpResponse get(string url, IDictionary<string, object?> options) => GetInternal(url, options);

    private ScriptHttpResponse GetInternal(string url, IDictionary<string, object?>? options)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (options is not null && options.TryGetValue("headers", out var headersObj) &&
            headersObj is IDictionary<string, object?> headers)
        {
            foreach (var (key, value) in headers)
                request.Headers.TryAddWithoutValidation(key, value?.ToString());
        }

        // Per-call timeout is opt-in inside the script; the engine's whole-script TimeoutInterval is the
        // hard ceiling regardless, so a hung call can never exceed the total budget.
        using var cts = new CancellationTokenSource();
        if (options is not null && options.TryGetValue("timeoutMs", out var t) && t is not null &&
            int.TryParse(t.ToString(), out var timeoutMs) && timeoutMs > 0)
            cts.CancelAfter(timeoutMs);

        // .GetAwaiter().GetResult() is deliberate — the JS call site is synchronous. The SSRF guard runs
        // inside SendAsync's connect, so a blocked host throws ScriptEgressBlockedException here.
        using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        var body = ReadCappedBody(response, cts.Token);
        return new ScriptHttpResponse(
            (int)response.StatusCode,
            body,
            TryParseJson(body),
            response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase));
    }

    private string ReadCappedBody(HttpResponseMessage response, CancellationToken ct)
    {
        using var stream = response.Content.ReadAsStream(ct);
        var buffer = new byte[Math.Min(maxResponseBytes, 64 * 1024)];
        using var ms = new MemoryStream();
        int read;
        while (ms.Length < maxResponseBytes && (read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            var allowed = (int)Math.Min(read, maxResponseBytes - ms.Length);
            ms.Write(buffer, 0, allowed);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static JsonNode? TryParseJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try { return JsonNode.Parse(body); }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// What a script sees from <c>http.get</c>: <c>r.statusCode</c>, <c>r.body</c>, <c>r.json</c> (null when
/// the body isn't JSON), <c>r.headers</c>. Public members are lowercase to read as idiomatic JS.
/// </summary>
internal sealed record ScriptHttpResponse(int statusCode, string body, JsonNode? json, IReadOnlyDictionary<string, string> headers);
