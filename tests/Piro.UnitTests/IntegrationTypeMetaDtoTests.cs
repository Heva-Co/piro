using FluentAssertions;
using Piro.Application.Extensions;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.UnitTests;

/// <summary>
/// Verifies IntegrationManifestExtensions.ToMetaDto — the projection that builds the wire format
/// exposed via GET /api/v1/integrations/types from each integration's IIntegration manifest (RFC 0016).
/// </summary>
public class IntegrationTypeMetaDtoTests
{
    [Fact]
    public void Jira_ExposesExpectedFieldsWithClientSecretMarkedSecret()
    {
        var dto = new Piro.Integrations.Jira.JiraIntegration().ToMetaDto();

        dto.Type.Should().Be("Jira");
        dto.Label.Should().Be("Jira");
        dto.Description.Should().NotBeNullOrEmpty();
        dto.IconifyIcon.Should().Be("logos:jira");
        dto.Direction.Should().Be(IntegrationDirection.Outbound);
        dto.ConfigSchema.Should().Contain(f => f.Key == "clientId" && f.Type == ConfigFieldType.String && f.Required && !f.IsSecret);
        dto.ConfigSchema.Should().Contain(f => f.Key == "clientSecret" && f.IsSecret && f.Required);
        dto.ConfigSchema.Should().Contain(f => f.Key == "defaultProjectKey" && !f.Required && !f.IsSecret);
    }

    [Fact]
    public void Ntfy_TokenFieldIsSecretAndNotRequired()
    {
        var dto = new Piro.Integrations.Ntfy.NtfyIntegration().ToMetaDto();

        var tokenField = dto.ConfigSchema.Single(f => f.Key == "token");
        tokenField.IsSecret.Should().BeTrue();
        tokenField.Required.Should().BeFalse();
    }

    [Fact]
    public void Telegram_DeclaresSendsPersonalNotificationCapability()
    {
        var dto = new Piro.Integrations.Telegram.TelegramIntegration().ToMetaDto();

        dto.Capabilities.Should().Contain(nameof(IntegrationCapability.SendsPersonalNotification));
    }

    [Fact]
    public void Jira_ClientSecretDoesNotSupportFileUpload()
    {
        var dto = new Piro.Integrations.Jira.JiraIntegration().ToMetaDto();

        var field = dto.ConfigSchema.Single(f => f.Key == "clientSecret");
        field.SupportsFileUpload.Should().BeFalse();
    }
}
