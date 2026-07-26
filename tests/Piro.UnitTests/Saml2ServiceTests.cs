using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Infrastructure.Auth;
using Xunit;

namespace Piro.UnitTests;

/// <summary>
/// Unit tests for the SAML2 sign-in service. These focus on the configuration and
/// security guards that don't require a fully crafted, signed SAMLResponse: certificate
/// validation on save, and the RelayState guard that defends the assertion-consumer step
/// against unsolicited/expired responses.
/// </summary>
public class Saml2ServiceTests
{
    // A throwaway self-signed cert (base64 DER) used only to exercise the parse path.
    private const string ValidBase64Cert =
        "MIIBpDCCAQ2gAwIBAgIUB1z+8mVJ7oQ0Xp0zvAqM8p0m0AwDQYJKoZIhvcNAQEL" +
        "BQAwEjEQMA4GA1UEAwwHVGVzdCBDQTAeFw0yMzAxMDEwMDAwMDBaFw0zMzAxMDEw" +
        "MDAwMDBaMBIxEDAOBgNVBAMMB1Rlc3QgQ0EwgZ8wDQYJKoZIhvcNAQEBBQADgY0A" +
        "MIGJAoGBAL9m6MmL0hqQ0mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0" +
        "mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0" +
        "mVZ0Zq0mVZ0Zq0mVZ0Zq0mVZ0Zq0AgMBAAEwDQYJKoZIhvcNAQELBQADgYEAKl==";

    private static Saml2Service BuildService(
        ISaml2ConfigRepository configRepo,
        ISsoUserProvisioner? provisioner = null,
        IDistributedCache? cache = null)
    {
        var siteRepo = Substitute.For<ISiteConfigRepository>();
        siteRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new SiteConfig(
                Name: null, Url: "https://piro.example.com", LogoUrl: null, FaviconUrl: null,
                MetaTitle: null, MetaDescription: null, OgImageUrl: null));

        var configuration = new ConfigurationBuilder().Build();

        return new Saml2Service(
            configRepo,
            siteRepo,
            configuration,
            cache ?? Substitute.For<IDistributedCache>(),
            provisioner ?? Substitute.For<ISsoUserProvisioner>());
    }

    [Fact]
    public async Task UpsertConfig_rejects_reserved_owner_id()
    {
        var repo = Substitute.For<ISaml2ConfigRepository>();
        var service = BuildService(repo);

        var request = new UpsertSaml2ProviderRequest(
            Id: "owner", DisplayName: "X", IdpEntityId: "urn:idp",
            IdpSsoUrl: "https://idp/sso", IdpSigningCertificate: ValidBase64Cert,
            SpEntityId: null, AllowedDomains: null, DefaultRole: "Member", IsEnabled: true);

        var act = async () => await service.UpsertConfigAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserved*");
    }

    [Fact]
    public async Task UpsertConfig_rejects_unparseable_certificate()
    {
        var repo = Substitute.For<ISaml2ConfigRepository>();
        repo.GetByIdAsync("okta", Arg.Any<CancellationToken>()).Returns((Saml2ProviderConfig?)null);
        var service = BuildService(repo);

        var request = new UpsertSaml2ProviderRequest(
            Id: "okta", DisplayName: "Okta", IdpEntityId: "urn:idp",
            IdpSsoUrl: "https://idp/sso", IdpSigningCertificate: "not-a-certificate",
            SpEntityId: null, AllowedDomains: null, DefaultRole: "Member", IsEnabled: true);

        var act = async () => await service.UpsertConfigAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*certificate could not be parsed*");
    }

    [Fact]
    public async Task UpsertConfig_requires_certificate_when_creating()
    {
        var repo = Substitute.For<ISaml2ConfigRepository>();
        repo.GetByIdAsync("okta", Arg.Any<CancellationToken>()).Returns((Saml2ProviderConfig?)null);
        var service = BuildService(repo);

        var request = new UpsertSaml2ProviderRequest(
            Id: "okta", DisplayName: "Okta", IdpEntityId: "urn:idp",
            IdpSsoUrl: "https://idp/sso", IdpSigningCertificate: null,
            SpEntityId: null, AllowedDomains: null, DefaultRole: "Member", IsEnabled: true);

        var act = async () => await service.UpsertConfigAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*certificate is required*");
    }

    [Fact]
    public async Task HandleAcs_rejects_expired_or_unknown_relay_state()
    {
        var repo = Substitute.For<ISaml2ConfigRepository>();
        var cache = Substitute.For<IDistributedCache>();
        // Cache miss → RelayState is unknown/expired.
        cache.GetAsync("saml:relay:abc", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var service = BuildService(repo, cache: cache);

        var act = async () => await service.HandleAcsAsync("<fake-saml-response>", "abc");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RelayState*");
    }

    [Fact]
    public async Task HandleAcs_rejects_empty_saml_response()
    {
        var repo = Substitute.For<ISaml2ConfigRepository>();
        var service = BuildService(repo);

        var act = async () => await service.HandleAcsAsync("", "abc");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Missing SAMLResponse*");
    }
}
