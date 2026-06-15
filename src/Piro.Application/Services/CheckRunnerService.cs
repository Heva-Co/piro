using Piro.Application.Interfaces;

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
    public async Task RunAsync(int checkId, CancellationToken ct = default)
    {
        var check = await checkRepo.GetByIdAsync(checkId, ct);
        if (check is null) return;

        await dispatcher.DispatchAsync(check, ct);
    }
}
