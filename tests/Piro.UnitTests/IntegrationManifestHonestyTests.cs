using System.Reflection;
using FluentAssertions;
using Piro.Contracts;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;
using Piro.Integrations.Abstractions;

namespace Piro.UnitTests;

/// <summary>
/// Manifest-honesty invariants (RFC 0016 §4.5): every shipped integration's <see cref="IntegrationManifest"/>
/// must not lie about itself. These are the checks that used to be spread across attribute-validation
/// tests before the enum was killed. Each concrete <see cref="IIntegration"/> is pure data with a
/// parameterless constructor (§4.3), so the test instantiates each and asserts against its manifest.
/// </summary>
public class IntegrationManifestHonestyTests
{
    // The valid catalog event keys a manifest's SupportedEvents may reference, plus wildcards.
    private static readonly HashSet<string> CatalogEventKeys =
        Enum.GetValues<NotificationEventType>().Select(e => e.WireName()).ToHashSet(StringComparer.Ordinal);

    // Every assembly that ships an IIntegration. All are copied into the test output dir because
    // Piro.Infrastructure references them, so Assembly.Load resolves each by name without the test
    // project needing a direct compile-time reference to all of them.
    private static readonly string[] IntegrationAssemblies =
    [
        "Piro.Integrations.Jira",
        "Piro.Integrations.Telegram",
        "Piro.Integrations.Twilio",
        "Piro.Integrations.Ntfy",
        "Piro.Integrations.GoogleChat",
        "Piro.Integrations.Webhook",
        "Piro.Integrations.Gcp",
        "Piro.Integrations.GoogleCloud",
        "Piro.Infrastructure", // Email integration lives here
    ];

    public static IEnumerable<object[]> AllIntegrations()
    {
        var assemblies = IntegrationAssemblies.Select(Assembly.Load).Distinct();

        foreach (var asm in assemblies)
        {
            foreach (var type in asm.GetTypes())
            {
                if (type is { IsAbstract: false, IsInterface: false } &&
                    typeof(IIntegration).IsAssignableFrom(type))
                {
                    var instance = (IIntegration)Activator.CreateInstance(type, nonPublic: true)!;
                    yield return [instance];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllIntegrations))]
    public void SubscribesToEvents_SetIff_SupportedEventsNonEmpty(IIntegration integration)
    {
        var m = integration.Manifest;
        var declaresCapability = m.Capabilities.HasFlag(IntegrationCapability.SubscribesToEvents);
        var hasEvents = m.SupportedEvents.Count > 0;

        declaresCapability.Should().Be(hasEvents,
            $"{integration.IntegrationId}: SubscribesToEvents must be set iff SupportedEvents is non-empty");
    }

    [Theory]
    [MemberData(nameof(AllIntegrations))]
    public void ProvidesChecks_SetIff_ProvidedChecksNonEmpty(IIntegration integration)
    {
        var declaresCapability = integration.Manifest.Capabilities.HasFlag(IntegrationCapability.ProvidesChecks);
        var shipsChecks = integration.ProvidedChecks().Any();

        declaresCapability.Should().Be(shipsChecks,
            $"{integration.IntegrationId}: ProvidesChecks must be set iff ProvidedChecks() is non-empty");
    }

    [Theory]
    [MemberData(nameof(AllIntegrations))]
    public void SupportedEvents_AreValidCatalogKeysOrWildcards(IIntegration integration)
    {
        foreach (var pattern in integration.Manifest.SupportedEvents)
        {
            var isValid = pattern == "*"
                || CatalogEventKeys.Contains(pattern)
                || (pattern.EndsWith(":*", StringComparison.Ordinal)
                    && CatalogEventKeys.Any(k => k.StartsWith(pattern[..^1], StringComparison.Ordinal)));

            isValid.Should().BeTrue(
                $"{integration.IntegrationId}: \"{pattern}\" must be a catalog event key or a matching wildcard");
        }
    }

    [Theory]
    [MemberData(nameof(AllIntegrations))]
    public void IntegrationId_IsNonEmpty_AndConfigTypeIsSet(IIntegration integration)
    {
        integration.IntegrationId.Should().NotBeNullOrWhiteSpace();
        integration.Manifest.ConfigType.Should().NotBeNull(
            $"{integration.IntegrationId}: a manifest must declare its ConfigType");
    }
}
