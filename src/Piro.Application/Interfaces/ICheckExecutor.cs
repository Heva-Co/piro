using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Executes a monitoring check of a specific type and returns a result.</summary>
public interface ICheckExecutor
{
    /// <summary>The check type this executor handles.</summary>
    CheckType CheckType { get; }

    Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default);
}
