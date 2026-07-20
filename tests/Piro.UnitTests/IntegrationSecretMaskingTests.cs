using FluentAssertions;
using Piro.Application.Integrations.Actions;
using NSubstitute;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

/// <summary>
/// Verifies that <see cref="IntegrationAppService"/> never returns plaintext credentials (masking on
/// read), encrypts every <c>[SecretField]</c> at rest for every type (not just PagerDuty), lets consumers
/// round-trip the ciphertext back to plaintext via <c>ReadDecryptedConfigJson</c>, and doesn't clobber a
/// stored secret when the client resubmits the masked placeholder unchanged — found during the RC audit:
/// ConfigJson (Slack bot tokens, PagerDuty routing keys, Jira API tokens, GoogleCloud service account
/// JSON, etc.) was previously returned in plaintext to any authenticated user and stored unencrypted.
/// </summary>
public class IntegrationSecretMaskingTests
{
    private static readonly Guid IntegrationId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid IntegrationId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IIntegrationRepository _repo = Substitute.For<IIntegrationRepository>();
    private readonly IWebhookRequestLogRepository _webhookLogRepo = Substitute.For<IWebhookRequestLogRepository>();
    private readonly IEscalationPolicyRepository _escalationPolicyRepo = Substitute.For<IEscalationPolicyRepository>();
    private readonly ISecretProtector _secretProtector = new FakeSecretProtector();
    private readonly IActionHost _actionHost = Substitute.For<IActionHost>();
    private readonly IActionRegistry _actionRegistry = Substitute.For<IActionRegistry>();
    private readonly IntegrationAppService _sut;

    public IntegrationSecretMaskingTests()
    {
        _sut = new IntegrationAppService(_repo, _webhookLogRepo, _escalationPolicyRepo, _secretProtector, _actionHost, _actionRegistry);
    }

    /// <summary>Deterministic protector for tests: prefixes ciphertext so round-trips are observable.</summary>
    private sealed class FakeSecretProtector : ISecretProtector
    {
        private const string Prefix = "enc:";
        public string Protect(string plaintext) => Prefix + plaintext;
        public string Unprotect(string ciphertext) => IsProtected(ciphertext) ? ciphertext[Prefix.Length..] : ciphertext;
        public bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetById_MasksSecretField()
    {
        var integration = new Integration
        {
            Id = IntegrationId1,
            Name = "Prod Jira",
            Type = IntegrationType.Jira,
            ConfigJson = """{"baseUrl":"https://x.atlassian.net","apiToken":"real-secret-value"}""",
        };
        _repo.GetByIdAsync(IntegrationId1, Arg.Any<CancellationToken>()).Returns(integration);

        var dto = await _sut.GetByIdAsync(IntegrationId1);

        dto.ConfigJson.Should().NotContain("real-secret-value");
        dto.ConfigJson.Should().Contain(IntegrationAppService.MaskedSecretValue);
    }

    [Fact]
    public async Task GetAll_MasksSecretFieldAcrossAllTypes()
    {
        var integrations = new[]
        {
            new Integration { Id = IntegrationId1, Name = "Jira", Type = IntegrationType.Jira, ConfigJson = """{"baseUrl":"https://x.atlassian.net","apiToken":"secret-token"}""" },
            new Integration { Id = IntegrationId2, Name = "GCP", Type = IntegrationType.GoogleCloud, ConfigJson = """{"serviceAccountJson":"{\"private_key\":\"secret\"}"}""" },
        };
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(integrations);

        var dtos = (await _sut.GetAllAsync()).ToList();

        dtos.Should().AllSatisfy(d => d.ConfigJson.Should().NotContain("secret"));
        dtos.Single(d => d.Id == IntegrationId1).ConfigJson.Should().Contain("https://x.atlassian.net"); // non-secret field preserved
    }

    [Fact]
    public async Task Update_ResubmittingMaskedValue_PreservesStoredSecret()
    {
        var integration = new Integration
        {
            Id = IntegrationId1,
            Name = "Jira",
            Type = IntegrationType.Jira,
            ConfigJson = """{"apiToken":"real-secret-value","projectKey":"OLD"}""",
        };
        _repo.GetByIdAsync(IntegrationId1, Arg.Any<CancellationToken>()).Returns(integration);
        _repo.UpdateAsync(Arg.Any<Integration>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Integration>());

        // Client fetched the masked DTO, changed nothing about the secret, and resubmitted the sentinel.
        var maskedConfigJson = $$"""{"apiToken":"{{IntegrationAppService.MaskedSecretValue}}","projectKey":"NEW"}""";
        var request = new UpdateIntegrationRequest(null, null, maskedConfigJson);

        await _sut.UpdateAsync(IntegrationId1, request);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Integration>(i => i.ConfigJson.Contains("real-secret-value") && i.ConfigJson.Contains("\"projectKey\":\"NEW\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithNewSecretValue_OverwritesStoredSecret()
    {
        var integration = new Integration
        {
            Id = IntegrationId1,
            Name = "Jira",
            Type = IntegrationType.Jira,
            ConfigJson = """{"apiToken":"old-secret","projectKey":"KEY"}""",
        };
        _repo.GetByIdAsync(IntegrationId1, Arg.Any<CancellationToken>()).Returns(integration);
        _repo.UpdateAsync(Arg.Any<Integration>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Integration>());

        var request = new UpdateIntegrationRequest(null, null, """{"apiToken":"new-secret","projectKey":"KEY"}""");

        await _sut.UpdateAsync(IntegrationId1, request);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Integration>(i => i.ConfigJson.Contains("new-secret") && !i.ConfigJson.Contains("old-secret")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_EncryptsSecretAtRest_ForEveryType_NotJustPagerDuty()
    {
        // Regression guard: encryption at rest used to be gated to PagerDuty only. Now every type
        // with a [SecretField] must have its secret encrypted before it reaches the repository.
        Integration? persisted = null;
        _repo.CreateAsync(Arg.Any<Integration>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Integration>())
            .AndDoes(ci => persisted = ci.Arg<Integration>());

        var request = new CreateIntegrationRequest(
            "Prod Jira", IntegrationType.Jira, null,
            """{"baseUrl":"https://x.atlassian.net","apiToken":"real-secret-value"}""", null);

        await _sut.CreateAsync(request);

        persisted.Should().NotBeNull();
        // Stored ciphertext: the secret is protected, the non-secret field stays plaintext.
        persisted!.ConfigJson.Should().NotContain("\"real-secret-value\"");
        persisted.ConfigJson.Should().Contain("enc:real-secret-value");
        persisted.ConfigJson.Should().Contain("https://x.atlassian.net");
    }

    [Fact]
    public void ReadDecryptedConfigJson_RoundTripsStoredSecret_ForConsumers()
    {
        // The centralized consumption read a dispatcher/executor uses: encrypted at rest → plaintext in-process.
        var stored = IntegrationExtensions.ProtectSecrets(
            IntegrationType.Jira,
            """{"baseUrl":"https://x.atlassian.net","apiToken":"real-secret-value"}""",
            _secretProtector);
        stored.Should().Contain("enc:real-secret-value");

        var integration = new Integration { Type = IntegrationType.Jira, ConfigJson = stored };

        var decrypted = integration.ReadDecryptedConfigJson(_secretProtector);

        decrypted.Should().Contain("\"real-secret-value\"");
        decrypted.Should().NotContain("enc:");
    }
}
