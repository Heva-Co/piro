using System.Reflection;
using FluentAssertions;
using Piro.Application.Integrations.Actions;
using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

/// <summary>
/// Guards the integration-action honesty contract (RFC 0012): the actions declared on an
/// IntegrationType via [IntegrationAction] and the registered IIntegrationAction handlers must agree —
/// every declared action has exactly one handler and vice versa — and the ProvidesActions capability
/// flag is set iff the type declares at least one action. Keeps manifest metadata and behavior from
/// silently drifting apart.
/// </summary>
public class IntegrationActionManifestTests
{
    /// <summary>All concrete IIntegrationAction implementations across the loaded assemblies.</summary>
    private static IReadOnlyList<IIntegrationAction> ResolveHandlers()
    {
        var infraAssembly = Assembly.Load("Piro.Infrastructure");
        return infraAssembly.GetTypes()
            .Where(t => typeof(IIntegrationAction).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => (IIntegrationAction)Activator.CreateInstance(t, NullArgsFor(t))!)
            .ToList();
    }

    // Handlers take only infrastructure services (e.g. IHttpClientFactory) that the manifest checks
    // never call — instantiate with nulls purely to read Type/ActionId.
    private static object?[] NullArgsFor(Type t) =>
        t.GetConstructors().First().GetParameters().Select(_ => (object?)null).ToArray();

    [Fact]
    public void EveryDeclaredActionHasExactlyOneHandler()
    {
        var handlerKeys = ResolveHandlers()
            .Select(h => (h.Type, h.ActionId))
            .ToList();

        foreach (var type in Enum.GetValues<IntegrationType>())
        {
            foreach (var declared in type.GetActions())
            {
                handlerKeys.Count(k => k.Type == type && k.ActionId == declared.ActionId)
                    .Should().Be(1, $"action '{declared.ActionId}' on {type} must have exactly one registered handler");
            }
        }
    }

    [Fact]
    public void EveryHandlerHasAMatchingDeclaredAction()
    {
        foreach (var handler in ResolveHandlers())
        {
            handler.Type.GetActions().Should().Contain(
                a => a.ActionId == handler.ActionId,
                $"handler for {handler.Type}/{handler.ActionId} must have a matching [IntegrationAction] on the manifest");
        }
    }

    [Fact]
    public void ProvidesActionsCapability_SetIffTypeDeclaresActions()
    {
        foreach (var type in Enum.GetValues<IntegrationType>())
        {
            var manifest = type.GetManifest();
            if (manifest is null) continue;

            var declaresActions = type.GetActions().Count > 0;
            var hasCapability = manifest.Capabilities.HasFlag(IntegrationCapability.ProvidesActions);

            hasCapability.Should().Be(declaresActions,
                $"{type}: ProvidesActions capability must be set iff it declares [IntegrationAction]s");
        }
    }
}
