using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Piro.Cli;

/// <summary>What the browser handed back on the loopback redirect.</summary>
internal sealed record LoginCallback(string? Code, string? State, string? Error);

/// <summary>
/// The browser half of <c>piro login</c> (RFC 0019 §4.6): listen on loopback, open the consent
/// screen, and wait for the redirect.
/// </summary>
/// <remarks>
/// A browser rather than a password prompt because an OIDC or SAML-only instance has no password for
/// a prompt to collect — and that is exactly the kind of deployment large enough to want config as
/// code. It also keeps the CLI out of the credential-handling business entirely.
/// </remarks>
internal sealed class LoginFlow : IDisposable
{
    private readonly HttpListener _listener = new();

    public string RedirectUri { get; }
    public string State { get; }
    public string CodeVerifier { get; }
    public string CodeChallenge { get; }

    public LoginFlow()
    {
        State = Base64Url(RandomNumberGenerator.GetBytes(32));
        CodeVerifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        CodeChallenge = Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(CodeVerifier)));

        // An ephemeral port, bound before the browser opens, so the port in the URL is one we
        // already hold. Explicitly 127.0.0.1 rather than a wildcard: binding all interfaces would
        // expose the callback beyond this machine.
        var port = FreePort();
        RedirectUri = $"http://127.0.0.1:{port}/callback";
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start() => _listener.Start();

    /// <summary>The URL of the consent screen, on the admin panel rather than the API.</summary>
    public string ConsentUrl(string baseUrl, string clientLabel) =>
        $"{baseUrl.TrimEnd('/')}/cli-auth"
        + $"?callback={Uri.EscapeDataString(RedirectUri)}"
        + $"&state={Uri.EscapeDataString(State)}"
        + $"&challenge={Uri.EscapeDataString(CodeChallenge)}"
        + $"&label={Uri.EscapeDataString(clientLabel)}";

    /// <summary>
    /// Waits for the browser to hit the callback, then serves a page telling the user the tab is safe
    /// to close. Times out rather than hanging forever if the user abandons the flow.
    /// </summary>
    public async Task<LoginCallback> WaitForCallbackAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(timeout);

        HttpListenerContext context;
        try
        {
            context = await _listener.GetContextAsync().WaitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CliException(
                $"Timed out after {timeout.TotalMinutes:0} minutes waiting for the browser. "
                + "Run `piro login` again, or set PIRO_API_KEY to authenticate without a browser.");
        }

        var query = context.Request.QueryString;
        var callback = new LoginCallback(query["code"], query["state"], query["error"]);

        await RespondAsync(context, callback);
        return callback;
    }

    /// <summary>
    /// Opens the system browser. Best-effort: if it fails, the caller has already printed the URL,
    /// which is also what makes this usable over SSH.
    /// </summary>
    public static bool TryOpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static async Task RespondAsync(HttpListenerContext context, LoginCallback callback)
    {
        var ok = callback.Error is null && callback.Code is not null;
        var body = Encoding.UTF8.GetBytes(Page(ok, callback.Error));

        context.Response.StatusCode = ok ? 200 : 400;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    /// <summary>
    /// Both outcomes are rendered. A user whose CLI already exited still needs to be told what
    /// happened and that the tab can be closed.
    /// </summary>
    private static string Page(bool ok, string? error)
    {
        var heading = ok ? "You're signed in" : "Sign-in failed";
        var message = ok
            ? "The Piro CLI has your session. You can close this tab."
            : WebUtility.HtmlEncode(error ?? "The request was cancelled.") + " You can close this tab.";

        return $$"""
            <!doctype html>
            <meta charset="utf-8">
            <title>Piro CLI</title>
            <style>
              body { font: 15px/1.6 system-ui, sans-serif; display: grid; place-items: center;
                     height: 100vh; margin: 0; color: #111; }
              .card { text-align: center; max-width: 26rem; padding: 2rem; }
              h1 { font-size: 1.1rem; margin: 0 0 .5rem; }
              p { color: #555; margin: 0; }
            </style>
            <div class="card">
              <h1>{{heading}}</h1>
              <p>{{message}}</p>
            </div>
            """;
    }

    /// <summary>Asks the OS for an unused port by binding one and releasing it immediately.</summary>
    private static int FreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose()
    {
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
    }
}
