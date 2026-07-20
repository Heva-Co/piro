using Piro.Domain.Enums;

namespace Piro.Application.Integrations.Actions;

/// <summary>A single selectable option for a dynamic-options field (RFC 0012).</summary>
public sealed record OptionItem(string Value, string Label);

/// <summary>
/// Supplies the runtime options for a <c>[DynamicOptions(sourceKey)]</c> field — the callback an
/// integration exposes so a select can be populated from the connected account (e.g. Jira projects /
/// issue types). Sibling to <see cref="IIntegrationAction"/>, resolved by (<see cref="Type"/>,
/// <see cref="SourceKey"/>), and reaches Piro only through <see cref="IActionHost"/> — never a repo or
/// the OAuth store directly. Any integration adds one of these + the attribute; the schema engine and
/// the form stay provider-agnostic.
/// </summary>
public interface IOptionsProvider
{
    /// <summary>Which integration type this provider belongs to (resolution discriminator).</summary>
    IntegrationType Type { get; }

    /// <summary>Stable source key matching the field's <c>[DynamicOptions(sourceKey)]</c> (e.g. "jira-projects").</summary>
    string SourceKey { get; }

    /// <summary>
    /// Resolves the options for this source. <paramref name="dependsOnValue"/> carries the current value
    /// of the cascade parent when the field declares <c>DependsOn</c> (e.g. the chosen project key when
    /// listing issue types), otherwise null.
    /// </summary>
    Task<IReadOnlyList<OptionItem>> GetOptionsAsync(
        IActionHost host, Guid integrationId, string? dependsOnValue, CancellationToken ct = default);
}
