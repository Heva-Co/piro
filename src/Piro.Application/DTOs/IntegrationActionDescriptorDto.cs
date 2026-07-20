namespace Piro.Application.DTOs;

/// <summary>
/// The wire shape the frontend renders one action button from (RFC 0012 §4.4). One descriptor per
/// (configured integration × ready action) whose contexts include the page's context. A not-ready
/// action is absent entirely — there is no "disabled" state, so no "requires connection" field.
/// </summary>
public sealed record IntegrationActionDescriptorDto(
    Guid IntegrationId,
    string IntegrationLabel,
    string ActionId,
    string Label,
    string? Description,
    string? IconifyIcon,
    bool HasInput,
    bool SupportsDraft,
    IReadOnlyList<ConfigFieldSchemaDto> InputSchema);
