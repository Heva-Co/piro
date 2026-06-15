using Piro.Domain.Enums;

namespace Piro.Application.Models;

/// <summary>Result produced by a single check executor run.</summary>
public record CheckExecutionResult(
    ServiceStatus Status,
    double? LatencyMs,
    string? ErrorMessage
);
