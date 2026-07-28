using System.Text.Json;
using System.Text.Json.Serialization;

namespace Piro.Cli;

/// <summary>A stored CLI session, keyed by the instance URL it belongs to.</summary>
internal sealed record StoredCredential(string Url, string RefreshToken, string? Email);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, StoredCredential>))]
internal sealed partial class CredentialJsonContext : JsonSerializerContext;

/// <summary>
/// Persists <c>piro login</c> sessions between invocations (RFC 0019 §4.6).
/// </summary>
/// <remarks>
/// <para>
/// The RFC calls for the OS credential store — Keychain, DPAPI, Secret Service — with a file
/// fallback. This implements the fallback only: a mode-0600 file under <c>~/.piro</c>. Per-platform
/// native interop is the largest source of platform-specific code in the CLI and is worth doing
/// separately, with the file path already proven.
/// </para>
/// <para>
/// Never <c>piro.config.yml</c>. That file is meant to be committed, which is exactly why the
/// credential lives somewhere else entirely rather than in an ignored key within it.
/// </para>
/// </remarks>
internal static class CredentialStore
{
    private static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".piro");

    private static string FilePath => Path.Combine(Directory, "credentials.json");

    public static StoredCredential? Find(string url)
    {
        var all = ReadAll();
        return all.TryGetValue(Key(url), out var credential) ? credential : null;
    }

    public static void Save(StoredCredential credential)
    {
        var all = ReadAll();
        all[Key(credential.Url)] = credential;
        WriteAll(all);
    }

    public static void Remove(string url)
    {
        var all = ReadAll();
        if (all.Remove(Key(url))) WriteAll(all);
    }

    private static Dictionary<string, StoredCredential> ReadAll()
    {
        if (!File.Exists(FilePath)) return new(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize(
                       json, CredentialJsonContext.Default.DictionaryStringStoredCredential)
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A corrupt store must not block a fresh login — the worst case is signing in again.
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void WriteAll(Dictionary<string, StoredCredential> all)
    {
        System.IO.Directory.CreateDirectory(Directory);

        // Created before writing so the token is never briefly world-readable on disk.
        if (!File.Exists(FilePath))
        {
            using (File.Create(FilePath)) { }
            Restrict(FilePath);
        }

        File.WriteAllText(FilePath,
            JsonSerializer.Serialize(all, CredentialJsonContext.Default.DictionaryStringStoredCredential));
        Restrict(FilePath);
    }

    /// <summary>Owner-only (0600). A no-op on Windows, where ACLs already restrict the profile.</summary>
    private static void Restrict(string path)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (IOException)
        {
            Console.Error.WriteLine(
                $"warning: could not restrict permissions on {path}. Check them yourself.");
        }
    }

    private static string Key(string url) => url.TrimEnd('/');
}
