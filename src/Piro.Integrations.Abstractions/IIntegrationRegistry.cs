namespace Piro.Integrations.Abstractions;

/// <summary>
/// The set of integrations Piro was built with, discovered from the explicit compile-time registry
/// (RFC 0016 §4.3) — the replacement for iterating a closed <c>IntegrationType</c> enum. Keyed by
/// <see cref="IIntegration.IntegrationId"/>. Not a runtime scan: the registry is populated from the
/// integration assemblies <c>Piro.Api</c> references, so the set is closed at build time yet open at
/// the type level (a string id, not an enum value).
/// </summary>
public interface IIntegrationRegistry
{
    /// <summary>All registered integrations, in no particular order.</summary>
    IReadOnlyList<IIntegration> All { get; }

    /// <summary>The integration with this id, or null if none is registered (e.g. an unknown/legacy id).</summary>
    IIntegration? Find(string integrationId);
}
