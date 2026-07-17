namespace Piro.Domain.Enums;

/// <summary>
/// Where an <see cref="Entities.Alert"/> came from. See RFC 0001 §4.4 — extensible for a future
/// inbound source without touching anything else.
/// </summary>
public enum AlertSource
{
    /// <summary>
    /// Generated internally by Piro's own <c>AlertEvaluationService</c> after a Check execution.
    /// </summary>
    Internal,

    /// <summary>
    /// Received via the GCP Cloud Monitoring webhook — see RFC 0001.
    /// </summary>
    GcpCloudMonitoring,
}
