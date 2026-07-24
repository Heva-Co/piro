using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Jira;

/// <summary>
/// The Jira "create-issue" action (RFC 0012 §4.6): creates a Jira Cloud issue for an Alert/Incident/
/// Maintenance and links it back. Authenticates with the OAuth bearer token from the host (never a raw
/// credential); routes through the 3LO gateway <c>api.atlassian.com/ex/jira/{cloudId}</c>. Targets Jira
/// Cloud REST v3 only (Server/DC wiki markup is out of scope).
/// <para>
/// Reaches Piro only through the <see cref="IIntegrationHost"/> (token + its own config) and the
/// resolved target on the <see cref="UIActionContext"/> ("Forma 1", RFC 0016) — it never loads the
/// Alert/Incident itself, gets its HttpClient from the host, and just returns the reference it created.
/// </para>
/// </summary>
public sealed class JiraCreateIssueAction : IUIAction
{
    public string IntegrationId => "Jira";
    public string ActionId => "create-issue";
    public string Label => "Create Jira ticket";
    public string? Description => "Create a Jira ticket and link it back to this object.";
    public string? IconifyIcon => "logos:jira";
    public IReadOnlyList<UISurface> Contexts => [UISurface.Alert, UISurface.Incident, UISurface.Maintenance];
    public bool HasInput => true;
    public bool SupportsDraft => true;
    public Type? InputType => typeof(JiraCreateIssueInput);

    /// <summary>Ready only when the integration has a live OAuth connection — an unconnected Jira shows no button (RFC 0012 §4.4).</summary>
    public Task<bool> IsReadyAsync(IIntegrationHost host, Guid integrationId, CancellationToken ct = default) =>
        host.IsOAuthConnectedAsync(integrationId, ct);

    public async Task<object?> BuildDraftAsync(IIntegrationHost host, UIActionContext ctx, CancellationToken ct = default)
    {
        var siteUrl = await host.GetConfigValueAsync(ctx.IntegrationId, "siteUrl", ct);
        var piroLink = string.IsNullOrWhiteSpace(siteUrl) ? ctx.Target.PiroUrl : $"{siteUrl.TrimEnd('/')}{ctx.Target.PiroUrl}";

        return new JiraCreateIssueInput
        {
            ProjectKey = await host.GetConfigValueAsync(ctx.IntegrationId, "defaultProjectKey", ct) ?? string.Empty,
            IssueType = await host.GetConfigValueAsync(ctx.IntegrationId, "defaultIssueType", ct) ?? string.Empty,
            Title = ctx.Target.Title,
            Description = $"{ctx.Target.Summary}\n\n[View in Piro]({piroLink})",
        };
    }

    public async Task<UIActionResult> ExecuteAsync(IIntegrationHost host, UIActionContext ctx, CancellationToken ct = default)
    {
        var input = ctx.Input as JiraCreateIssueInput
            ?? throw new InvalidOperationException("Jira create-issue requires a JiraCreateIssueInput.");

        var cloudId = await host.GetConfigValueAsync(ctx.IntegrationId, "cloudId", ct)
            ?? throw new InvalidOperationException("Jira integration has no cloudId — reconnect the integration.");
        var siteUrl = await host.GetConfigValueAsync(ctx.IntegrationId, "siteUrl", ct);
        var token = await host.GetBearerTokenAsync(ctx.IntegrationId, ct);

        var http = host.GetRequiredService<HttpClient>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new JsonObject
        {
            ["fields"] = new JsonObject
            {
                ["project"] = new JsonObject { ["key"] = input.ProjectKey },
                ["issuetype"] = new JsonObject { ["name"] = input.IssueType },
                ["summary"] = input.Title,
                ["description"] = MarkdownToAdf.Convert(input.Description),
            },
        };

        var url = $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue";
        var response = await http.PostAsJsonAsync(url, body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Jira issue creation failed ({(int)response.StatusCode}): {errorBody}");
        }

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var key = created.TryGetProperty("key", out var k) ? k.GetString() : null;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Jira returned no issue key.");

        var browseUrl = string.IsNullOrWhiteSpace(siteUrl)
            ? $"https://api.atlassian.com/ex/jira/{cloudId}/browse/{key}"
            : $"{siteUrl.TrimEnd('/')}/browse/{key}";

        return new UIActionResult(key, browseUrl, key);
    }
}
