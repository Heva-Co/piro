namespace Piro.Application.DTOs;

/// <summary>A saved Service↔Integration mapping, for the match UI (RFC 0004 §4.5).</summary>
public record ServiceIntegrationMappingDto(
    int ServiceId,
    Guid IntegrationId,
    string? RemoteId,
    string? RemoteLabel);

/// <summary>Request to map a Piro service to a discovered remote resource.</summary>
public record UpsertServiceIntegrationMappingRequest(
    Guid IntegrationId,
    string RemoteId);
