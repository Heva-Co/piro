using FluentAssertions;
using NSubstitute;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

/// <summary>
/// Verifies that <see cref="IntegrationAppService"/> never returns plaintext credentials and that
/// updates don't clobber a stored secret when the client resubmits the masked placeholder unchanged —
/// found during the RC audit: ConfigJson (Slack bot tokens, PagerDuty routing keys, Jira API tokens,
/// GoogleCloud service account JSON, etc.) was previously returned in plaintext to any authenticated user.
/// </summary>
public class IntegrationSecretMaskingTests
{
    private readonly IIntegrationRepository _repo = Substitute.For<IIntegrationRepository>();
    private readonly IntegrationAppService _sut;

    public IntegrationSecretMaskingTests()
    {
        _sut = new IntegrationAppService(_repo);
    }

    [Fact]
    public async Task GetById_MasksSecretField()
    {
        var integration = new Integration
        {
            Id = 1,
            Name = "Prod PagerDuty",
            Type = IntegrationType.PagerDuty,
            ConfigJson = """{"routingKey":"real-secret-value"}""",
        };
        _repo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(integration);

        var dto = await _sut.GetByIdAsync(1);

        dto.ConfigJson.Should().NotContain("real-secret-value");
        dto.ConfigJson.Should().Contain(IntegrationAppService.MaskedSecretValue);
    }

    [Fact]
    public async Task GetAll_MasksSecretFieldAcrossAllTypes()
    {
        var integrations = new[]
        {
            new Integration { Id = 1, Name = "Jira", Type = IntegrationType.Jira, ConfigJson = """{"baseUrl":"https://x.atlassian.net","apiToken":"secret-token"}""" },
            new Integration { Id = 2, Name = "GCP", Type = IntegrationType.GoogleCloud, ConfigJson = """{"serviceAccountJson":"{\"private_key\":\"secret\"}"}""" },
        };
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(integrations);

        var dtos = (await _sut.GetAllAsync()).ToList();

        dtos.Should().AllSatisfy(d => d.ConfigJson.Should().NotContain("secret"));
        dtos.Single(d => d.Id == 1).ConfigJson.Should().Contain("https://x.atlassian.net"); // non-secret field preserved
    }

    [Fact]
    public async Task Update_ResubmittingMaskedValue_PreservesStoredSecret()
    {
        var integration = new Integration
        {
            Id = 1,
            Name = "Opsgenie",
            Type = IntegrationType.Opsgenie,
            ConfigJson = """{"apiKey":"real-secret-value","region":"US"}""",
        };
        _repo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(integration);
        _repo.UpdateAsync(Arg.Any<Integration>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Integration>());

        // Client fetched the masked DTO, changed nothing about the secret, and resubmitted the sentinel.
        var maskedConfigJson = $$"""{"apiKey":"{{IntegrationAppService.MaskedSecretValue}}","region":"EU"}""";
        var request = new UpdateIntegrationRequest(null, null, maskedConfigJson);

        await _sut.UpdateAsync(1, request);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Integration>(i => i.ConfigJson.Contains("real-secret-value") && i.ConfigJson.Contains("\"region\":\"EU\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithNewSecretValue_OverwritesStoredSecret()
    {
        var integration = new Integration
        {
            Id = 1,
            Name = "Opsgenie",
            Type = IntegrationType.Opsgenie,
            ConfigJson = """{"apiKey":"old-secret","region":"US"}""",
        };
        _repo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(integration);
        _repo.UpdateAsync(Arg.Any<Integration>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Integration>());

        var request = new UpdateIntegrationRequest(null, null, """{"apiKey":"new-secret","region":"US"}""");

        await _sut.UpdateAsync(1, request);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Integration>(i => i.ConfigJson.Contains("new-secret") && !i.ConfigJson.Contains("old-secret")),
            Arg.Any<CancellationToken>());
    }
}
