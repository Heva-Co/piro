using Piro.Application.Integrations.Actions;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Integrations.Actions;

/// <summary>
/// The Jira "create-issue" action (RFC 0012 §4.6). This commit wires up only the parts needed to make
/// the button appear and its dialog open — discovery metadata, input schema, and a draft. Readiness is
/// provisional (returns true for any configured Jira integration); the real "OAuth-connected" gate and
/// the live Jira call arrive with the OAuth commit. <see cref="ExecuteAsync"/> is intentionally not
/// implemented yet.
/// </summary>
internal sealed class JiraCreateIssueAction : IIntegrationAction
{
    public IntegrationType Type => IntegrationType.Jira;
    public string ActionId => "create-issue";
    public Type? InputType => typeof(JiraCreateIssueInput);

    // TODO(oauth commit): gate on a live OAuth token (host.GetBearerTokenAsync succeeds) so an
    // unconnected Jira integration shows no button, per RFC 0012 §4.4. Provisional: always ready.
    public Task<bool> IsReadyAsync(IActionHost host, Guid integrationId, CancellationToken ct = default) =>
        Task.FromResult(true);

    public async Task<object?> BuildDraftAsync(IActionHost host, ActionExecutionContext ctx, CancellationToken ct = default)
    {
        var target = await host.GetTargetAsync(ctx.Context, ctx.TargetId, ct);
        if (target is null) return null;

        return new JiraCreateIssueInput
        {
            Title = target.Title,
            Description = target.Summary,
        };
    }

    public Task<ActionResult> ExecuteAsync(IActionHost host, ActionExecutionContext ctx, CancellationToken ct = default) =>
        throw new NotImplementedException("Jira create-issue execution lands with the OAuth + handler commit (RFC 0012 Phase 1).");
}
