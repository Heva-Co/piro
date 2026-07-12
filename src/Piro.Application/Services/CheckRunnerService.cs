using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Application.Services;

/// <summary>Runs a check by loading the check entity and handing off to <see cref="ICheckJobDispatcher"/>.</summary>
/// <remarks>
/// Called by the Quartz scheduler job per check execution. Execution logic,
/// result persistence, and status propagation are handled by the dispatcher and
/// <see cref="ICheckResultIngester"/> respectively.
/// </remarks>
public class CheckRunnerService(
    ICheckRepository checkRepo,
    ICheckJobDispatcher dispatcher)
{
    /// <summary>
    /// Dispatches execution for <paramref name="checkId"/> and returns the check as loaded
    /// before dispatch. For multi-region checks, results arrive asynchronously via worker
    /// callbacks — the returned entity's status may not yet reflect this run.
    /// </summary>
    public async Task<Check?> RunAsync(int checkId, CancellationToken ct = default)
    {
        var check = await checkRepo.GetByIdAsync(checkId, ct);
        if (check is null) return null;

        await dispatcher.DispatchAsync(check, ct);
        return check;
    }
}
