using Piro.Integrations.MobilePush.Relay;

namespace Piro.UnitTests;

/// <summary>
/// The register URL is derived from the push URL so the operator supplies one address instead of two.
/// Getting that derivation wrong would POST an invite to the wrong place, and an invite is single-use —
/// so these cases are worth pinning.
/// </summary>
public class RelayInviteRedeemerTests
{
    [Theory]
    // The relay shares a process with the Socket.IO service, so in some deployments it sits under a
    // path prefix rather than at the root.
    [InlineData("https://api.dev.heva.pro/socket.io/v1/push", "https://api.dev.heva.pro/socket.io/v1/register")]
    [InlineData("https://relay.heva.co/v1/push", "https://relay.heva.co/v1/register")]
    [InlineData("https://relay.heva.co/v1/push/", "https://relay.heva.co/v1/register")]
    [InlineData("  https://relay.heva.co/v1/push  ", "https://relay.heva.co/v1/register")]
    [InlineData("http://localhost:3000/v1/push", "http://localhost:3000/v1/register")]
    public void TryResolveRegisterUrl_DerivesTheRegisterEndpoint(string pushUrl, string expected)
    {
        Assert.True(RelayInviteRedeemer.TryResolveRegisterUrl(pushUrl, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("https://relay.heva.co/v1/register")]  // already the register endpoint
    [InlineData("https://relay.heva.co/")]             // no /push segment to rewrite
    public void TryResolveRegisterUrl_RejectsWhatIsNotAPushUrl(string? pushUrl)
    {
        Assert.False(RelayInviteRedeemer.TryResolveRegisterUrl(pushUrl, out _));
    }

    [Theory]
    [InlineData("inv_abc123", true)]
    [InlineData("hvr_abc123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LooksLikeInvite_MatchesOnlyTheInvitePrefix(string? value, bool expected)
    {
        Assert.Equal(expected, RelayInviteRedeemer.LooksLikeInvite(value));
    }

    [Theory]
    [InlineData("hvr_abc123", true)]
    [InlineData("inv_abc123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LooksLikeApiKey_MatchesOnlyTheKeyPrefix(string? value, bool expected)
    {
        Assert.Equal(expected, RelayInviteRedeemer.LooksLikeApiKey(value));
    }
}
