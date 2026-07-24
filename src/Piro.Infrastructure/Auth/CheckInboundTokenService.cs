using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Auth;

/// <summary>
/// <see cref="ICheckInboundTokenService"/> over <see cref="ApiKeyService"/> (RFC 0013): the Application
/// seam for minting and reading a check's scoped inbound token. Agnostic to the check type.
/// </summary>
internal sealed class CheckInboundTokenService(ApiKeyService apiKeys) : ICheckInboundTokenService
{
    public Task<string> CreateOrRotateAsync(int checkId, CancellationToken ct = default) =>
        apiKeys.CreateOrRotateCheckInboundKeyAsync(checkId, ct);

    public async Task<CheckInboundTokenInfo?> GetInfoAsync(int checkId, CancellationToken ct = default)
    {
        var info = await apiKeys.GetCheckInboundKeyInfoAsync(checkId, ct);
        return info is { } i ? new CheckInboundTokenInfo(i.MaskedKey, i.LastUsedAt) : null;
    }
}
