using System.Collections.Concurrent;

namespace Piro.Infrastructure.Integrations.GoogleCloud;

/// <summary>Singleton token cache for GCP service account tokens, keyed by IntegrationId.</summary>
public class GcpTokenCache
{
    private readonly ConcurrentDictionary<int, (string Token, DateTime Expiry)> _cache = new();

    public bool TryGet(int integrationId, out string token)
    {
        if (_cache.TryGetValue(integrationId, out var entry) && entry.Expiry > DateTime.UtcNow)
        {
            token = entry.Token;
            return true;
        }
        token = string.Empty;
        return false;
    }

    public void Set(int integrationId, string token, DateTime expiry) =>
        _cache[integrationId] = (token, expiry);
}
