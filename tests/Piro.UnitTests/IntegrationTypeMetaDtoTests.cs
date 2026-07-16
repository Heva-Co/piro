using FluentAssertions;
using Piro.Application.Extensions;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

/// <summary>
/// Verifies IntegrationManifestExtensions.ToMetaDto — the reflection step that builds the wire
/// format exposed via GET /api/v1/integrations/types (RFC 0003).
/// </summary>
public class IntegrationTypeMetaDtoTests
{
    [Fact]
    public void ObsoleteType_HasNoMetaDto()
    {
        var dto = IntegrationType.Webhook.ToMetaDto();

        dto.Should().BeNull();
    }

    [Fact]
    public void Jira_ExposesExpectedFieldsWithApiTokenMarkedSecret()
    {
        var dto = IntegrationType.Jira.ToMetaDto();

        dto.Should().NotBeNull();
        dto!.Type.Should().Be("Jira");
        dto.Label.Should().Be("Jira");
        dto.Description.Should().NotBeNullOrEmpty();
        dto.IconifyIcon.Should().Be("logos:jira");
        dto.Direction.Should().Be(IntegrationDirection.Outbound);
        dto.ConfigSchema.Should().Contain(f => f.Key == "baseUrl" && f.Type == ConfigFieldType.Url && f.Required);
        dto.ConfigSchema.Should().Contain(f => f.Key == "apiToken" && f.IsSecret && f.Required);
        dto.ConfigSchema.Should().Contain(f => f.Key == "projectKey" && f.Type == ConfigFieldType.String && !f.IsSecret);
    }

    [Fact]
    public void EveryNonObsoleteType_HasLabelDescriptionAndIcon()
    {
        foreach (var type in Enum.GetValues<IntegrationType>())
        {
            var dto = type.ToMetaDto();
            if (dto is null)
                continue; // obsolete types have no manifest — covered by ObsoleteType_HasNoMetaDto

            dto.Label.Should().NotBeNullOrEmpty($"{type} should declare a Label");
            dto.Description.Should().NotBeNullOrEmpty($"{type} should declare a Description");
            dto.IconifyIcon.Should().NotBeNullOrEmpty($"{type} should declare an IconifyIcon");
        }
    }

    [Fact]
    public void Ntfy_TokenFieldIsSecretAndNotRequired()
    {
        var dto = IntegrationType.Ntfy.ToMetaDto();

        var tokenField = dto!.ConfigSchema.Single(f => f.Key == "token");
        tokenField.IsSecret.Should().BeTrue();
        tokenField.Required.Should().BeFalse();
    }

    [Fact]
    public void GoogleCloud_DeclaresRequiredByCheckTypeCapability()
    {
        var dto = IntegrationType.GoogleCloud.ToMetaDto();

        dto!.Capabilities.Should().Contain(nameof(IntegrationCapability.RequiredByCheckType));
        dto.Capabilities.Should().NotContain(nameof(IntegrationCapability.SendsPersonalNotification));
    }

    [Fact]
    public void Telegram_DeclaresSendsPersonalNotificationCapability()
    {
        var dto = IntegrationType.Telegram.ToMetaDto();

        dto!.Capabilities.Should().ContainSingle().Which.Should().Be(nameof(IntegrationCapability.SendsPersonalNotification));
    }

    [Fact]
    public void Opsgenie_RegionIsAnEnumFieldWithFixedOptions()
    {
        var dto = IntegrationType.Opsgenie.ToMetaDto();

        var regionField = dto!.ConfigSchema.Single(f => f.Key == "region");
        regionField.Type.Should().Be(ConfigFieldType.Enum);
        regionField.IsSecret.Should().BeFalse();
        regionField.Options.Should().BeEquivalentTo(["US", "EU"]);
    }

    [Fact]
    public void GoogleCloud_ServiceAccountJsonIsMultilineAndSecret()
    {
        var dto = IntegrationType.GoogleCloud.ToMetaDto();

        var field = dto!.ConfigSchema.Single(f => f.Key == "serviceAccountJson");
        field.Type.Should().Be(ConfigFieldType.Multiline);
        field.IsSecret.Should().BeTrue();
        field.SupportsFileUpload.Should().BeTrue();
    }

    [Fact]
    public void Jira_ApiTokenDoesNotSupportFileUpload()
    {
        var dto = IntegrationType.Jira.ToMetaDto();

        var field = dto!.ConfigSchema.Single(f => f.Key == "apiToken");
        field.SupportsFileUpload.Should().BeFalse();
    }
}
