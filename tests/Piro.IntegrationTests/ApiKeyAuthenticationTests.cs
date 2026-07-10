using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Piro.Domain.Entities;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// End-to-end verification that an API key created through the authenticated JWT flow
/// actually authenticates subsequent requests via the X-Api-Key header — the gap found
/// during the Configuration -> API Keys audit (ValidateAsync existed but nothing called it).
/// </summary>
public class ApiKeyAuthenticationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly string _ownerEmail = $"owner-{Guid.NewGuid():N}@test.local";
    private readonly string _viewerEmail = $"viewer-{Guid.NewGuid():N}@test.local";
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Program.cs uses WebApplication.CreateBuilder(args) with top-level statements, which
        // reads configuration before WebApplicationFactory's ConfigureAppConfiguration hook runs.
        // Environment variables are read by CreateBuilder itself, so they reliably override
        // appsettings.Development.json regardless of host-builder configuration order.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTest");
        Environment.SetEnvironmentVariable("Database__ConnectionString", _container.GetConnectionString());
        Environment.SetEnvironmentVariable("Auth__JwtSecret", "integration-test-secret-at-least-32-bytes-long!!");
        Environment.SetEnvironmentVariable("Auth__AccessTokenExpiryMinutes", "60");

        _factory = new WebApplicationFactory<Program>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Piro.Infrastructure.Persistence.PiroDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        foreach (var role in new[] { "Owner", "Viewer" })
            if (await roleManager.FindByNameAsync(role) is null)
                await roleManager.CreateAsync(new AppRole { Name = role });

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var owner = new AppUser { UserName = _ownerEmail, Email = _ownerEmail, Name = "Test Owner", IsActive = true, EmailConfirmed = true };
        var createOwnerResult = await userManager.CreateAsync(owner, "Sup3rSecret!Password");
        if (!createOwnerResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", createOwnerResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(owner, "Owner");

        var viewer = new AppUser { UserName = _viewerEmail, Email = _viewerEmail, Name = "Test Viewer", IsActive = true, EmailConfirmed = true };
        var createViewerResult = await userManager.CreateAsync(viewer, "Sup3rSecret!Password");
        if (!createViewerResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", createViewerResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(viewer, "Viewer");

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private async Task<string> SignInAndGetJwtAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-in", new
        {
            email,
            password = "Sup3rSecret!Password",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SignInResponseDto>();
        return body!.AccessToken;
    }

    [Fact]
    public async Task CreatedApiKey_AuthenticatesSubsequentRequest()
    {
        var jwt = await SignInAndGetJwtAsync(_ownerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/auth/api-keys", new { name = "CI key" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiKeyCreatedDto>();

        // Fresh client — no JWT at all, only the raw API key
        using var apiKeyClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var protectedResponse = await apiKeyClient.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task RevokedApiKey_NoLongerAuthenticates()
    {
        var jwt = await SignInAndGetJwtAsync(_ownerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/auth/api-keys", new { name = "Revoke me" });
        var created = await createResponse.Content.ReadFromJsonAsync<ApiKeyCreatedDto>();

        var revokeResponse = await _client.DeleteAsync($"/api/v1/auth/api-keys/{created!.Id}");
        revokeResponse.EnsureSuccessStatusCode();

        using var apiKeyClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created.RawKey);
        var protectedResponse = await apiKeyClient.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task NoApiKeyAndNoJwt_IsUnauthorized()
    {
        using var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await anonClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ViewerRole_IsForbiddenFromManagingApiKeys()
    {
        var jwt = await SignInAndGetJwtAsync(_viewerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/auth/api-keys", new { name = "Viewer key" });
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        var listResponse = await _client.GetAsync("/api/v1/auth/api-keys");
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        var deleteResponse = await _client.DeleteAsync("/api/v1/auth/api-keys/1");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    private record SignInResponseDto(string AccessToken, string RefreshToken, int ExpiresIn);
    private record ApiKeyCreatedDto(int Id, string Name, string RawKey, string MaskedKey, DateTime CreatedAt);
}
