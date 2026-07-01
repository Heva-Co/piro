using Piro.Domain.Enums;

namespace Piro.Application.Attributes;

/// <summary>
/// Marks a check executor as requiring a provider Integration of the specified type.
/// Used by the GET /api/v1/checks/types endpoint to inform the frontend which integration
/// types must exist before a check type becomes available.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequiresIntegrationAttribute(IntegrationType integrationType) : Attribute
{
    public IntegrationType IntegrationType { get; } = integrationType;
}
