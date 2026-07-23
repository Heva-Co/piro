using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Piro.Application.Integrations.Actions;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Integrations.Actions;

/// <summary>
/// Shared helper for the Jira dynamic-options providers (RFC 0012): builds a bearer-authed client
/// pointed at the 3LO gateway for the integration's cloudId.
/// </summary>
internal static class JiraOptionsHttp
{
    public const string HttpClientName = "oauth-integration-http";

    public static async Task<(HttpClient Http, string CloudId)> CreateAsync(
        IActionHost host, IHttpClientFactory factory, Guid integrationId, CancellationToken ct)
    {
        var cloudId = await host.GetConfigValueAsync(integrationId, "cloudId", ct)
            ?? throw new InvalidOperationException("Jira integration has no cloudId — reconnect the integration.");
        var token = await host.GetBearerTokenAsync(integrationId, ct);
        var http = factory.CreateClient(HttpClientName);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (http, cloudId);
    }
}

/// <summary>Lists the connected Jira account's projects (RFC 0012) — populates the "Project key" select.</summary>
internal sealed class JiraProjectsOptionsProvider(IHttpClientFactory httpClientFactory) : IOptionsProvider
{
    public string IntegrationId => "Jira";
    public string SourceKey => "jira-projects";

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(
        IActionHost host, Guid integrationId, string? dependsOnValue, CancellationToken ct = default)
    {
        var (http, cloudId) = await JiraOptionsHttp.CreateAsync(host, httpClientFactory, integrationId, ct);

        var options = new List<OptionItem>();
        var startAt = 0;
        bool isLast;
        do
        {
            var url = $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/project/search?startAt={startAt}&maxResults=50";
            var page = await http.GetFromJsonAsync<JsonElement>(url, ct);

            if (page.TryGetProperty("values", out var values))
            {
                foreach (var project in values.EnumerateArray())
                {
                    var key = project.GetProperty("key").GetString()!;
                    var name = project.TryGetProperty("name", out var n) ? n.GetString() ?? key : key;
                    options.Add(new OptionItem(key, $"{name} ({key})"));
                }
            }

            isLast = !page.TryGetProperty("isLast", out var last) || last.GetBoolean();
            startAt += 50;
        } while (!isLast);

        return options;
    }
}

/// <summary>
/// Lists issue types for a chosen Jira project (RFC 0012) — populates the "Issue type" select, cascading
/// off the selected project key. Returns nothing until a project is chosen.
/// </summary>
internal sealed class JiraIssueTypesOptionsProvider(IHttpClientFactory httpClientFactory) : IOptionsProvider
{
    public string IntegrationId => "Jira";
    public string SourceKey => "jira-issue-types";

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(
        IActionHost host, Guid integrationId, string? dependsOnValue, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dependsOnValue))
            return [];

        var (http, cloudId) = await JiraOptionsHttp.CreateAsync(host, httpClientFactory, integrationId, ct);

        // createmeta scoped to the project returns exactly the issue types creatable there.
        var url = $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue/createmeta?projectKeys={Uri.EscapeDataString(dependsOnValue)}&expand=projects.issuetypes";
        var meta = await http.GetFromJsonAsync<JsonElement>(url, ct);

        var options = new List<OptionItem>();
        if (meta.TryGetProperty("projects", out var projects))
        {
            foreach (var project in projects.EnumerateArray())
            {
                if (!project.TryGetProperty("issuetypes", out var issueTypes)) continue;
                foreach (var issueType in issueTypes.EnumerateArray())
                {
                    var name = issueType.GetProperty("name").GetString()!;
                    options.Add(new OptionItem(name, name));
                }
            }
        }

        return options;
    }
}
