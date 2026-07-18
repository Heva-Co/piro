namespace Piro.Application.DTOs;

/// <summary>Typed PagerDuty mapping — the shape stored in ServiceIntegrationMapping.MappingJson for
/// a PagerDuty integration (RFC 0004 §4.5).</summary>
public record PagerDutyMappingData(
    string PagerDutyServiceId,
    string RoutingKey,
    string? PagerDutyServiceName);
