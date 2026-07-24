namespace Piro.Integrations.Abstractions;

/// <summary>A single selectable option for a dynamic-options field (RFC 0012).</summary>
public sealed record OptionItem(string Value, string Label);

/// <summary>
/// Supplies the runtime options for a <c>[DynamicOptions(sourceKey)]</c> field — the callback an
/// integration exposes so a select can be populated from the connected account (e.g. Jira projects /
/// issue types). Sibling to <see cref="IUIAction"/>, resolved by (<see cref="IntegrationId"/>,
/// <see cref="SourceKey"/>). It reaches Piro only through the root <see cref="IIntegrationHost"/> — the
/// OAuth token and its own config — never a repository or the OAuth store directly. The integration
/// registers it imperatively at startup, alongside its actions.
/// </summary>
public interface IOptionsProvider
{
    /// <summary>Stable integration id this provider belongs to (e.g. "Jira").</summary>
    string IntegrationId { get; }

    /// <summary>Stable source key matching the field's <c>[DynamicOptions(sourceKey)]</c> (e.g. "jira-projects").</summary>
    string SourceKey { get; }

    /// <summary>
    /// Resolves the options for this source. <paramref name="dependsOnValue"/> carries the current value
    /// of the cascade parent when the field declares <c>DependsOn</c> (e.g. the chosen project key when
    /// listing issue types), otherwise null.
    /// </summary>
    Task<IReadOnlyList<OptionItem>> GetOptionsAsync(
        IIntegrationHost host, Guid integrationId, string? dependsOnValue, CancellationToken ct = default);
}
