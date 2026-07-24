using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// The concrete integration registry (RFC 0016 §4.3): the set of integrations Piro was built with,
/// resolved from the <see cref="IIntegration"/> instances DI discovered from the referenced
/// <c>Piro.Integrations.*</c> assemblies. Indexed by <see cref="IIntegration.IntegrationId"/>.
/// </summary>
internal sealed class IntegrationRegistry(IEnumerable<IIntegration> integrations) : IIntegrationRegistry
{
    private readonly Dictionary<string, IIntegration> _byId =
        integrations.ToDictionary(i => i.IntegrationId, StringComparer.Ordinal);

    public IReadOnlyList<IIntegration> All => _byId.Values.ToList();

    public IIntegration? Find(string integrationId) =>
        _byId.GetValueOrDefault(integrationId);
}
