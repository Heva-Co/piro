namespace Piro.Infrastructure.Integrations.GoogleCloud;

public interface IGcpTokenProvider
{
    Task<string> GetAccessTokenAsync(int integrationId, string configJson, CancellationToken ct = default);
}
