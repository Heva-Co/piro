namespace Piro.Domain.Enums;

/// <summary>
/// Wire-level shape of a ConfigJson field's input — derived by reflection from the Data
/// Annotations and CLR type present on the corresponding property of a config type (an
/// IntegrationType's manifest ConfigType, or a CheckType's *CheckConfig record — RFC 0011),
/// never hand-assigned. See IntegrationManifestAttribute and ConfigSchemaBuilder.
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

    // --- Added by RFC 0011 for Check config forms (notification configs are scalar-only). ---

    /// <summary>
    /// A numeric input — from an int/long/double property (e.g. a check's Port or TimeoutMs).
    /// </summary>
    Number,

    /// <summary>
    /// A checkbox — from a bool property (e.g. an HTTP check's FollowRedirects).
    /// </summary>
    Boolean,

    /// <summary>
    /// An add/remove list of strings — from a List&lt;string&gt; property (e.g. a DNS check's NameServers).
    /// </summary>
    StringList,

    /// <summary>
    /// An add/remove list of key/value pairs — from a Dictionary&lt;string,string&gt; property (e.g. an HTTP check's Headers).
    /// </summary>
    KeyValue,

    /// <summary>
    /// An add/remove list of nested objects — from a List&lt;T&gt; property whose element is a record
    /// (e.g. an HTTP check's ResponseRules). The element's own field schema is carried on
    /// ConfigFieldSchemaDto.ItemSchema and rendered recursively.
    /// </summary>
    ObjectArray,

    /// <summary>
    /// A code editor — for a property carrying arbitrary source (e.g. a Script check's Script). See CodeFieldAttribute.
    /// </summary>
    Code,
}
