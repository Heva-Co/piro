using FluentAssertions;
using Piro.Domain;

namespace Piro.UnitTests;

public class TagValidationTests
{
    [Theory]
    [InlineData("piro:region")]      // full system namespace
    [InlineData("piro:anything")]
    [InlineData("piro")]             // the bare reserved root — must be rejected too (issue #203 follow-up)
    public void ValidateUserKey_RejectsReservedNamespace(string key)
    {
        TagValidation.ValidateUserKey(key)
            .Should().Contain("reserved", "the piro namespace (and its bare root) belongs to system tags");
    }

    [Theory]
    [InlineData("env")]
    [InlineData("team-payments")]
    [InlineData("piroish")]          // starts with "piro" but is not the reserved root nor the "piro:" namespace
    [InlineData("tier_1")]
    public void ValidateUserKey_AcceptsValidUserKeys(string key)
    {
        TagValidation.ValidateUserKey(key).Should().BeNull();
    }
}
