namespace Piro.Domain.Enums;

/// <summary>
/// Wire-level shape of a ConfigJson field's input — derived by reflection from the Data
/// Annotations present on the corresponding property of an IntegrationType's manifest ConfigType,
/// never hand-assigned. See IntegrationManifestAttribute.
/// Orthogonal to whether the field is secret (see ConfigFieldSchemaDto.IsSecret) — a field can be
/// e.g. Multiline and secret at once (GoogleCloudConfig.ServiceAccountJson), or String and secret
/// (JiraConfig.ApiToken), or Enum and not secret (OpsgenieConfig.Region).
/// </summary>
public enum ConfigFieldType
{
    String,
    Url,
    Email,
    Enum,
    Multiline,
}
