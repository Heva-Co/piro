using System.Text.RegularExpressions;

namespace Piro.Domain;

/// <summary>
/// Syntactic rules for user-supplied tag keys and values (RFC 0008 §4.2). Pure functions with no
/// dependencies so they can be reused by the app service, and later by the Part C lint pass.
/// </summary>
public static partial class TagValidation
{
    /// <summary>A user key: starts with a lowercase letter, then lowercase alphanumerics, <c>-</c> or <c>_</c>.</summary>
    [GeneratedRegex("^[a-z][a-z0-9_-]*$")]
    private static partial Regex UserKeyPattern();

    /// <summary>
    /// Returns null if the key is a valid user key, otherwise a human-readable reason it is rejected.
    /// Rejects the reserved <c>piro:</c> namespace, over-length keys, and malformed keys.
    /// </summary>
    public static string? ValidateUserKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "A tag key cannot be empty.";
        if (key.StartsWith(TagConstants.SystemNamespace, StringComparison.Ordinal))
            return $"The '{TagConstants.SystemNamespace}' namespace is reserved for system tags.";
        if (key.Length > TagConstants.MaxKeyLength)
            return $"Tag key '{key}' exceeds the maximum length of {TagConstants.MaxKeyLength}.";
        if (!UserKeyPattern().IsMatch(key))
            return $"Tag key '{key}' must start with a lowercase letter and contain only lowercase letters, digits, '-' and '_'.";
        return null;
    }

    /// <summary>Returns null if the value is acceptable (null/empty allowed), otherwise a rejection reason.</summary>
    public static string? ValidateValue(string key, string? value)
    {
        if (value is not null && value.Length > TagConstants.MaxValueLength)
            return $"The value for tag key '{key}' exceeds the maximum length of {TagConstants.MaxValueLength}.";
        return null;
    }
}
