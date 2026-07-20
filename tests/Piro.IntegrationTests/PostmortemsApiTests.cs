using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// End-to-end verification of the postmortem report API (RFC 0005, Phase 1): create seeds a value per
/// standard field, incident linking surfaces a derived timeline, publish/unpublish flips status, and
/// field values persist through update.
/// </summary>
public class PostmortemsApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly string _ownerEmail = $"owner-{Guid.NewGuid():N}@test.local";
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private int _ownerUserId;

    // Mirror the API's wire format: camelCase properties, enums as strings (Program.cs).
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTest");
        Environment.SetEnvironmentVariable("Database__ConnectionString", _container.GetConnectionString());
        Environment.SetEnvironmentVariable("Auth__JwtSecret", "integration-test-secret-at-least-32-bytes-long!!");
        Environment.SetEnvironmentVariable("Auth__AccessTokenExpiryMinutes", "60");

        _factory = new WebApplicationFactory<Program>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Piro.Infrastructure.Persistence.PiroDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        if (await roleManager.FindByNameAsync("Owner") is null)
            await roleManager.CreateAsync(new AppRole { Name = "Owner" });

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var owner = new AppUser { UserName = _ownerEmail, Email = _ownerEmail, Name = "Test Owner", IsActive = true, EmailConfirmed = true };
        var result = await userManager.CreateAsync(owner, "Sup3rSecret!Password");
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(owner, "Owner");
        _ownerUserId = owner.Id;

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var jwt = await SignInAndGetJwtAsync(_ownerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Create_SeedsAValuePerStandardField_AndSnapshotsOwnerName()
    {
        var created = await CreatePostmortemAsync("Q3 outage review", _ownerUserId);

        Assert.Equal("Q3 outage review", created!.Name);
        Assert.Equal(PostmortemStatus.Draft, created.Status);
        Assert.Equal("Test Owner", created.ReviewOwnerName);
        Assert.Equal(_ownerUserId, created.ReviewOwnerUserId);
        // Eight seeded standard sections, each with an empty value.
        Assert.Equal(8, created.Fields.Count());
        Assert.Contains(created.Fields, f => f.Key == "root_causes" && f.Value == "");
        Assert.All(created.Fields, f => Assert.True(f.IsSystem));
    }

    [Fact]
    public async Task Update_PersistsFieldValues()
    {
        var created = await CreatePostmortemAsync("Cache stampede", _ownerUserId);
        var rootCauses = created!.Fields.First(f => f.Key == "root_causes");

        var updateResp = await _client.PutAsJsonAsync($"/api/v1/postmortems/{created.Id}", new
        {
            fields = new[] { new { fieldDefinitionId = rootCauses.FieldDefinitionId, value = "Thundering herd on cold cache." } },
        });
        updateResp.EnsureSuccessStatusCode();

        var reloaded = await _client.GetFromJsonAsync<PostmortemResponse>($"/api/v1/postmortems/{created.Id}", Json);
        Assert.Equal("Thundering herd on cold cache.",
            reloaded!.Fields.First(f => f.Key == "root_causes").Value);
    }

    [Fact]
    public async Task LinkIncident_SurfacesDerivedTimeline()
    {
        // A resolved incident with a status-change timeline event.
        int incidentId = await SeedIncidentAsync("DB failover", IncidentStatus.Resolved);

        var created = await CreatePostmortemAsync("Failover retro", _ownerUserId);

        var linkResp = await _client.PostAsJsonAsync(
            $"/api/v1/postmortems/{created!.Id}/incidents", new { incidentId });
        linkResp.EnsureSuccessStatusCode();
        var linked = await linkResp.Content.ReadFromJsonAsync<PostmortemResponse>(Json);

        Assert.Single(linked!.Incidents);
        Assert.Equal("DB failover", linked.Incidents.First().Title);
        // The "Created" timeline event of the incident is derived into the report's timeline.
        Assert.Contains(linked.Timeline, t => t.IncidentId == incidentId && t.Source.StartsWith("incident:"));

        // Unlink removes both the reference and its derived timeline entries.
        var unlinkResp = await _client.DeleteAsync($"/api/v1/postmortems/{created.Id}/incidents/{incidentId}");
        unlinkResp.EnsureSuccessStatusCode();
        var unlinked = await unlinkResp.Content.ReadFromJsonAsync<PostmortemResponse>(Json);
        Assert.Empty(unlinked!.Incidents);
        Assert.Empty(unlinked.Timeline);
    }

    [Fact]
    public async Task PublishThenUnpublish_FlipsStatus()
    {
        var created = await CreatePostmortemAsync("Publish flow", _ownerUserId);

        (await _client.PostAsync($"/api/v1/postmortems/{created!.Id}/publish", null)).EnsureSuccessStatusCode();
        var published = await _client.GetFromJsonAsync<PostmortemResponse>($"/api/v1/postmortems/{created.Id}", Json);
        Assert.Equal(PostmortemStatus.Published, published!.Status);
        Assert.NotNull(published.PublishedAt);

        (await _client.PostAsync($"/api/v1/postmortems/{created.Id}/unpublish", null)).EnsureSuccessStatusCode();
        var reverted = await _client.GetFromJsonAsync<PostmortemResponse>($"/api/v1/postmortems/{created.Id}", Json);
        Assert.Equal(PostmortemStatus.Draft, reverted!.Status);
        Assert.Null(reverted.PublishedAt);
    }

    [Fact]
    public async Task LinkNonexistentIncident_Returns404()
    {
        var created = await CreatePostmortemAsync("Bad link", _ownerUserId);
        var resp = await _client.PostAsJsonAsync(
            $"/api/v1/postmortems/{created!.Id}/incidents", new { incidentId = 999_999 });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<PostmortemResponse?> CreatePostmortemAsync(string name, int? ownerId)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/postmortems", new
        {
            name,
            reviewOwnerUserId = ownerId,
        });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PostmortemResponse>(Json);
    }

    private async Task<int> SeedIncidentAsync(string title, IncidentStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Piro.Infrastructure.Persistence.PiroDbContext>();
        var incident = new Incident
        {
            Title = title,
            StartDateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = status,
            Source = "MANUAL",
            TimelineEvents =
            {
                new IncidentTimelineEvent
                {
                    Type = TimelineEventType.Created,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Visibility = EventVisibility.Private,
                },
            },
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        return incident.Id;
    }

    private async Task<string> SignInAndGetJwtAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = "Sup3rSecret!Password" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SignInResponseDto>(Json);
        return body!.AccessToken;
    }

    private record SignInResponseDto(string AccessToken, string RefreshToken, int ExpiresIn);

    private record PostmortemResponse(
        int Id,
        string Name,
        PostmortemStatus Status,
        int? ReviewOwnerUserId,
        string? ReviewOwnerName,
        DateTimeOffset? PublishedAt,
        IEnumerable<FieldResponse> Fields,
        IEnumerable<IncidentRefResponse> Incidents,
        IEnumerable<TimelineItemResponse> Timeline);

    private record FieldResponse(int FieldDefinitionId, string Key, string Heading, bool IsSystem, string Value);
    private record IncidentRefResponse(int IncidentId, string Title, IncidentStatus Status);
    private record TimelineItemResponse(int IncidentId, string IncidentTitle, string Source, DateTimeOffset OccurredAt);
}
