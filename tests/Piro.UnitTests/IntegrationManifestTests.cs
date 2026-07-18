using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Piro.Domain.Attributes;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

/// <summary>
/// Guards the IntegrationManifest contract (RFC 0003): every non-obsolete IntegrationType must
/// declare a manifest, its ConfigType must be a real class whose declared ConfigJson fields are
/// internally consistent, and the set of types marked SendsPersonalNotification must match the
/// actual IPersonalNotificationDispatcher registrations in InfrastructureServiceExtensions — so the
/// two don't silently drift apart over time.
/// </summary>
public class IntegrationManifestTests
{
    /// <summary>Mirrors the IPersonalNotificationDispatcher registrations in InfrastructureServiceExtensions.</summary>
    private static readonly HashSet<IntegrationType> RegisteredDispatcherTypes =
    [
        IntegrationType.Email,
        IntegrationType.Telegram,
        IntegrationType.Twilio,
        IntegrationType.Ntfy,
    ];

    /// <summary>Mirrors the ISystemEventDispatcher registrations in InfrastructureServiceExtensions (RFC 0004).</summary>
    private static readonly HashSet<IntegrationType> RegisteredSystemEventDispatcherTypes =
    [
        IntegrationType.PagerDuty,
    ];

    private static IEnumerable<IntegrationType> NonObsoleteTypes() =>
        Enum.GetValues<IntegrationType>().Where(t => !IsObsolete(t));

    private static bool IsObsolete(IntegrationType type) =>
        typeof(IntegrationType)
            .GetField(type.ToString())!
            .GetCustomAttributes(typeof(ObsoleteAttribute), false)
            .Length > 0;

    [Fact]
    public void EveryNonObsoleteType_HasAManifest()
    {
        foreach (var type in NonObsoleteTypes())
            type.GetManifest().Should().NotBeNull($"{type} should declare an IntegrationManifest");
    }

    [Fact]
    public void EveryManifest_ConfigTypeIsAConcreteClassWithNoConstructorArgs()
    {
        foreach (var type in NonObsoleteTypes())
        {
            var configType = type.GetManifest()!.ConfigType;
            configType.IsClass.Should().BeTrue($"{type}'s ConfigType should be a class");
            configType.GetConstructor(Type.EmptyTypes).Should().NotBeNull($"{type}'s ConfigType should have a parameterless constructor for JsonSerializer.Deserialize");
        }
    }

    [Fact]
    public void SendsPersonalNotificationCapability_MatchesActualDispatcherRegistrations()
    {
        foreach (var type in NonObsoleteTypes())
        {
            var declaresCapability = type.GetManifest()!.Capabilities.HasFlag(IntegrationCapability.SendsPersonalNotification);
            var isRegistered = RegisteredDispatcherTypes.Contains(type);

            declaresCapability.Should().Be(isRegistered,
                $"{type}'s manifest {(declaresCapability ? "declares" : "does not declare")} SendsPersonalNotification, " +
                $"but a dispatcher is {(isRegistered ? "" : "not ")}registered for it — these must stay in sync");
        }
    }

    [Fact]
    public void SendsAlertEventsCapability_MatchesActualDispatcherRegistrations()
    {
        foreach (var type in NonObsoleteTypes())
        {
            var declaresCapability = type.GetManifest()!.Capabilities.HasFlag(IntegrationCapability.SendsAlertEvents);
            var isRegistered = RegisteredSystemEventDispatcherTypes.Contains(type);

            declaresCapability.Should().Be(isRegistered,
                $"{type}'s manifest {(declaresCapability ? "declares" : "does not declare")} SendsAlertEvents, " +
                $"but an ISystemEventDispatcher is {(isRegistered ? "" : "not ")}registered for it — these must stay in sync");
        }
    }

    [Theory]
    [InlineData(IntegrationType.Jira, "ApiToken")]
    [InlineData(IntegrationType.GoogleCloud, "ServiceAccountJson")]
    [InlineData(IntegrationType.Telegram, "BotToken")]
    [InlineData(IntegrationType.Twilio, "AuthToken")]
    [InlineData(IntegrationType.Ntfy, "Token")]
    public void SecretFieldAttribute_IsPresentOnExpectedProperty(IntegrationType type, string propertyName)
    {
        var configType = type.GetManifest()!.ConfigType;
        var property = configType.GetProperty(propertyName);

        property.Should().NotBeNull();
        property!.GetCustomAttributes(typeof(SecretFieldAttribute), false).Should().HaveCount(1);
    }

    [Fact]
    public void RequiredConfigProperties_AreMarkedWithRequiredAttribute()
    {
        // Spot-check: Jira's non-secret identifying fields must still be required, matching the
        // frontend form's validation (JiraConfig.tsx / integrationFormSchema).
        var jiraConfigType = IntegrationType.Jira.GetManifest()!.ConfigType;
        var baseUrl = jiraConfigType.GetProperty("BaseUrl")!;

        baseUrl.GetCustomAttributes(typeof(RequiredAttribute), false).Should().NotBeEmpty();
    }
}
