using Piro.Application.Models;
using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>
/// Runs a monitoring check and returns its result. A single implementation resolves the right check
/// from the registry by the check's type discriminator, deserializes its config, runs the probe, and
/// maps the raw outcome to a <see cref="CheckExecutionResult"/> — there is no per-type executor.
/// </summary>
public interface ICheckExecutor
{
    Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default);
}
