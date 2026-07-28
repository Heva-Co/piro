using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Infrastructure.Auth;
using Piro.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// Security coverage for the <c>piro login</c> code exchange (RFC 0019 §4.6, §8).
/// </summary>
/// <remarks>
/// A loopback listener is reachable by every local process, so PKCE, single use, the short TTL,
/// state, and loopback-only callbacks are load-bearing rather than defense in depth. Each of these
/// tests removes one of those and asserts the exchange fails.
/// </remarks>
public class CliAuthServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private string _connectionString = null!;
    private FakeTimeProvider _clock = null!;
    private AppUser _user = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var db = NewContext();
        await db.Database.MigrateAsync();

        _user = new AppUser { UserName = "cli@test.local", Email = "cli@test.local", Name = "CLI" };
        db.Users.Add(_user);
        await db.SaveChangesAsync();

        _clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-27T12:00:00Z"));
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private PiroDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PiroDbContext>().UseNpgsql(_connectionString).Options);

    private CliAuthService NewService(PiroDbContext db) =>
        new(db, _clock, NullLogger<CliAuthService>.Instance);

    private const string Callback = "http://127.0.0.1:51234/callback";

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        return (verifier, Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<(string Code, string Verifier)> IssueAsync(string callback = Callback)
    {
        var (verifier, challenge) = Pkce();
        await using var db = NewContext();
        var user = await db.Users.FirstAsync();
        var code = await NewService(db).IssueCodeAsync(
            user, new CliAuthorizeRequest(callback, challenge, "state-123", "piro-cli on laptop"));
        return (code, verifier);
    }

    private async Task<(AppUser User, string? ClientLabel)?> RedeemAsync(
        string code, string verifier, string callback = Callback)
    {
        await using var db = NewContext();
        return await NewService(db).RedeemCodeAsync(new CliTokenRequest(code, verifier, callback));
    }

    [Fact]
    public async Task AValidExchange_ReturnsTheUserAndItsLabel()
    {
        var (code, verifier) = await IssueAsync();

        var result = await RedeemAsync(code, verifier);

        result.Should().NotBeNull();
        result!.Value.User.Email.Should().Be("cli@test.local");
        result.Value.ClientLabel.Should().Be("piro-cli on laptop");
    }

    [Fact]
    public async Task TheRawCodeIsNeverStored()
    {
        var (code, _) = await IssueAsync();

        await using var db = NewContext();
        var row = await db.CliAuthorizationCodes.SingleAsync();

        row.CodeHash.Should().NotBe(code);
        row.CodeHash.Should().HaveLength(64); // SHA-256 hex
    }

    [Fact]
    public async Task AWrongVerifier_IsRejected()
    {
        // Without PKCE, stealing the code off the loopback redirect would be enough.
        var (code, _) = await IssueAsync();
        var (otherVerifier, _) = Pkce();

        (await RedeemAsync(code, otherVerifier)).Should().BeNull();
    }

    [Fact]
    public async Task AWrongVerifierBurnsTheCode()
    {
        // The code is consumed on first presentation, so a local process that stole it cannot sit
        // there brute-forcing the verifier against the same code.
        var (code, verifier) = await IssueAsync();

        (await RedeemAsync(code, Pkce().Verifier)).Should().BeNull();
        (await RedeemAsync(code, verifier)).Should().BeNull();
    }

    [Fact]
    public async Task ACodeIsSingleUse()
    {
        var (code, verifier) = await IssueAsync();

        (await RedeemAsync(code, verifier)).Should().NotBeNull();
        (await RedeemAsync(code, verifier)).Should().BeNull();
    }

    [Fact]
    public async Task AnExpiredCodeIsRejected()
    {
        var (code, verifier) = await IssueAsync();

        _clock.Advance(TimeSpan.FromMinutes(6));

        (await RedeemAsync(code, verifier)).Should().BeNull();
    }

    [Fact]
    public async Task RedeemingAgainstADifferentCallbackIsRejected()
    {
        // The code is bound to the callback it was minted for, so it cannot be replayed elsewhere.
        var (code, verifier) = await IssueAsync();

        (await RedeemAsync(code, verifier, "http://127.0.0.1:9999/callback")).Should().BeNull();
    }

    [Fact]
    public async Task AnUnknownCodeIsRejected()
    {
        (await RedeemAsync("not-a-real-code", Pkce().Verifier)).Should().BeNull();
    }

    [Theory]
    [InlineData("https://evil.example.com/callback")]
    [InlineData("http://169.254.169.254/callback")]
    [InlineData("http://127.0.0.1.evil.com/callback")]
    [InlineData("piro://callback")]
    [InlineData("not a url")]
    public async Task ANonLoopbackCallbackCannotBeIssued(string callback)
    {
        // The consent screen is a phishing target: a link with an attacker-controlled callback would
        // forward the token if this allowlist ever loosened.
        await using var db = NewContext();
        var user = await db.Users.FirstAsync();
        var service = NewService(db);

        service.IsLoopback(callback).Should().BeFalse();

        var act = () => service.IssueCodeAsync(
            user, new CliAuthorizeRequest(callback, Pkce().Challenge, "state", null));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("http://127.0.0.1:51234/callback")]
    [InlineData("http://localhost:8080/callback")]
    [InlineData("http://[::1]:51234/callback")]
    public async Task LoopbackCallbacksAreAccepted(string callback)
    {
        await using var db = NewContext();
        NewService(db).IsLoopback(callback).Should().BeTrue();
    }

    [Fact]
    public async Task AMissingChallengeIsRejected()
    {
        await using var db = NewContext();
        var user = await db.Users.FirstAsync();

        var act = () => NewService(db).IssueCodeAsync(
            user, new CliAuthorizeRequest(Callback, "", "state", null));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
