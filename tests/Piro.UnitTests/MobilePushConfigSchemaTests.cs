using System.Text.Json;
using System.Text.Json.Serialization;
using Piro.Contracts;
using Piro.Integrations.MobilePush;
using Piro.Integrations.MobilePush.Transport;

namespace Piro.UnitTests;

/// <summary>
/// The MobilePush form is rendered generically from this schema, so what the admin panel shows is
/// decided here rather than in the frontend. These pin the two things that make the form usable: the
/// delivery mode is a select, and the credentials for the mode you are not using stay hidden.
/// </summary>
public class MobilePushConfigSchemaTests
{
    private static ConfigFieldSchemaDto Field(string key) =>
        ConfigSchemaBuilder.For(typeof(MobilePushConfig)).Single(f => f.Key == key);

    [Fact]
    public void Mode_RendersAsAnEnumSelectWithBothModes()
    {
        var mode = Field("mode");

        // Without this the field renders as an empty free-text box, which is what a CLR enum used to do.
        Assert.Equal(ConfigFieldType.Enum, mode.Type);
        Assert.NotNull(mode.Options);
        Assert.Contains("Direct", mode.Options!);
        Assert.Contains("Relay", mode.Options!);
    }

    [Fact]
    public void Mode_DefaultsToDirectSoAnUpgradeKeepsWorking()
    {
        // An existing deployment already has FCM/APNs credentials configured. Defaulting to Relay would
        // silently stop its push on upgrade.
        Assert.Equal(nameof(PushTransportMode.Direct), Field("mode").Default?.ToString());
    }

    [Theory]
    [InlineData("fcmServiceAccountJson")]
    [InlineData("apnsPrivateKey")]
    [InlineData("apnsKeyId")]
    [InlineData("apnsTeamId")]
    [InlineData("apnsBundleId")]
    [InlineData("apnsProduction")]
    public void DirectCredentials_AreHiddenUnlessModeIsDirect(string key)
    {
        var field = Field(key);

        Assert.NotNull(field.VisibleWhen);
        Assert.Equal("mode", field.VisibleWhen!.Field);
        Assert.Equal(["Direct"], field.VisibleWhen.Values);
    }

    [Fact]
    public void RelayApiKey_IsSecretSoItIsEncryptedAtRest()
    {
        // The issued key grants send rights against Heva's provider identities; it must never be
        // stored or returned in the clear.
        Assert.True(Field("relayApiKey").IsSecret);
    }

    [Fact]
    public void RelayAppId_IsNotSecret_SoItCanBeShownForSupport()
    {
        Assert.False(Field("relayAppId").IsSecret);
        Assert.False(Field("relayKeyId").IsSecret);
    }

    [Fact]
    public void StoredConfigJson_WithModeAsAName_Deserializes()
    {
        // The admin form saves the enum by name (that is what the schema publishes as its options), and
        // IntegrationHost reads config with Web defaults. Web defaults alone reject a named enum, so
        // reading a saved MobilePush config threw until a string-enum converter was added.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        var config = JsonSerializer.Deserialize<MobilePushConfig>(
            """{"mode": "Relay", "fcmServiceAccountJson": null}""", options);

        Assert.NotNull(config);
        Assert.Equal(PushTransportMode.Relay, config!.Mode);
    }

    [Fact]
    public void StoredConfigJson_WithNoMode_FallsBackToDirect()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        // A config saved before the mode field existed must keep sending the way it always did.
        var config = JsonSerializer.Deserialize<MobilePushConfig>("""{"apnsKeyId":"ABC"}""", options);

        Assert.Equal(PushTransportMode.Direct, config!.Mode);
    }
}
