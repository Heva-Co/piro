namespace Piro.Domain;

/// <summary>
/// Shared limits and reserved names for the tag system (RFC 0008). Named constants, not magic numbers, so
/// the same bounds are enforced identically across validation, the API, and the admin UI.
/// </summary>
public static class TagConstants
{
    /// <summary>Maximum number of tags a single entity may carry.</summary>
    public const int MaxTagsPerEntity = 50;

    /// <summary>Reserved namespace for Piro-owned system keys; rejected from user input.</summary>
    public const string SystemNamespace = "piro:";

    /// <summary>The reserved namespace root without its separator ("piro"). Rejected as an exact key too,
    /// so a user can't claim the bare namespace word (only "piro:*" system keys may use it).</summary>
    public const string SystemNamespaceRoot = "piro";

    /// <summary>Maximum key length, matching common label conventions.</summary>
    public const int MaxKeyLength = 63;

    /// <summary>Maximum value length. A value is optional; when present it is bounded like the key.</summary>
    public const int MaxValueLength = 255;
}
