using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Piro.Domain.Tags;

namespace Piro.UnitTests;

public class TagSelectorEvaluatorTests
{
    private static IReadOnlyDictionary<string, string?> Tags(params (string Key, string? Value)[] pairs)
    {
        var d = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    [Fact]
    public void EmptySelector_MatchesEverything()
    {
        TagSelectorEvaluator.Matches(new TagSelector(), Tags(("env", "prod"))).Should().BeTrue();
        TagSelectorEvaluator.Matches(new TagSelector(), Tags()).Should().BeTrue();
    }

    [Fact]
    public void Equals_MatchesOnlyExactValue()
    {
        var sel = new TagSelector(AllOf: [new TagTerm("env", TagOp.Equals, ["prod"])]);
        TagSelectorEvaluator.Matches(sel, Tags(("env", "prod"))).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("env", "staging"))).Should().BeFalse();
        TagSelectorEvaluator.Matches(sel, Tags()).Should().BeFalse();
    }

    [Fact]
    public void Equals_OnKeyOnlyTag_DoesNotMatch()
    {
        // a key-only tag (null value) never Equals a concrete value
        var sel = new TagSelector(AllOf: [new TagTerm("critical", TagOp.Equals, ["true"])]);
        TagSelectorEvaluator.Matches(sel, Tags(("critical", null))).Should().BeFalse();
    }

    [Fact]
    public void In_MatchesAnyOfTheValues()
    {
        var sel = new TagSelector(AllOf: [new TagTerm("region", TagOp.In, ["eu", "us"])]);
        TagSelectorEvaluator.Matches(sel, Tags(("region", "eu"))).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("region", "asia"))).Should().BeFalse();
        TagSelectorEvaluator.Matches(sel, Tags()).Should().BeFalse();
    }

    [Fact]
    public void NotIn_MissingKey_Matches()
    {
        // k8s DoesNotExist intuition: a missing key satisfies NotIn
        var sel = new TagSelector(AllOf: [new TagTerm("region", TagOp.NotIn, ["eu"])]);
        TagSelectorEvaluator.Matches(sel, Tags()).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("region", "us"))).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("region", "eu"))).Should().BeFalse();
    }

    [Fact]
    public void Exists_MatchesPresentKeyRegardlessOfValue()
    {
        var sel = new TagSelector(AllOf: [new TagTerm("critical", TagOp.Exists)]);
        TagSelectorEvaluator.Matches(sel, Tags(("critical", null))).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("critical", "yes"))).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("other", "x"))).Should().BeFalse();
    }

    [Fact]
    public void AllOf_RequiresEveryTerm()
    {
        var sel = new TagSelector(AllOf:
        [
            new TagTerm("env", TagOp.Equals, ["prod"]),
            new TagTerm("tier", TagOp.Equals, ["critical"]),
        ]);
        TagSelectorEvaluator.Matches(sel, Tags(("env", "prod"), ("tier", "critical"))).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("env", "prod"))).Should().BeFalse();
    }

    [Fact]
    public void AnyOf_RequiresAtLeastOneTerm()
    {
        var sel = new TagSelector(AnyOf:
        [
            new TagTerm("env", TagOp.Equals, ["prod"]),
            new TagTerm("tier", TagOp.Equals, ["critical"]),
        ]);
        TagSelectorEvaluator.Matches(sel, Tags(("tier", "critical"))).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("env", "dev"))).Should().BeFalse();
    }

    [Fact]
    public void AllOfAndAnyOf_AreAndedTogether()
    {
        var sel = new TagSelector(
            AllOf: [new TagTerm("env", TagOp.Equals, ["prod"])],
            AnyOf: [new TagTerm("region", TagOp.In, ["eu", "us"])]);
        TagSelectorEvaluator.Matches(sel, Tags(("env", "prod"), ("region", "eu"))).Should().BeTrue();
        TagSelectorEvaluator.Matches(sel, Tags(("env", "prod"), ("region", "asia"))).Should().BeFalse();
        TagSelectorEvaluator.Matches(sel, Tags(("env", "dev"), ("region", "eu"))).Should().BeFalse();
    }

    [Fact]
    public void RoundTrips_ThroughJson_WithStringEnum()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var sel = new TagSelector(
            AllOf: [new TagTerm("env", TagOp.In, ["prod", "staging"])],
            AnyOf: [new TagTerm("critical", TagOp.Exists)]);

        var json = JsonSerializer.Serialize(sel, options);
        json.Should().Contain("\"In\"").And.Contain("\"Exists\"");

        var back = JsonSerializer.Deserialize<TagSelector>(json, options)!;
        TagSelectorEvaluator.Matches(back, Tags(("env", "prod"), ("critical", null))).Should().BeTrue();
    }
}

public class TagSelectorValidationTests
{
    [Fact]
    public void Validate_AcceptsWellFormedSelector()
    {
        var sel = new TagSelector(AllOf: [new TagTerm("env", TagOp.Equals, ["prod"])]);
        TagSelectorValidation.Validate(sel).Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsEmptyKey()
    {
        var sel = new TagSelector(AllOf: [new TagTerm("", TagOp.Exists)]);
        TagSelectorValidation.Validate(sel).Should().NotBeNull();
    }

    [Theory]
    [InlineData(TagOp.In)]
    [InlineData(TagOp.NotIn)]
    [InlineData(TagOp.Equals)]
    public void Validate_RejectsValuedOperatorWithoutValues(TagOp op)
    {
        var sel = new TagSelector(AllOf: [new TagTerm("env", op, null)]);
        TagSelectorValidation.Validate(sel).Should().NotBeNull();
    }

    [Fact]
    public void Validate_RejectsEqualsWithMultipleValues()
    {
        var sel = new TagSelector(AllOf: [new TagTerm("env", TagOp.Equals, ["a", "b"])]);
        TagSelectorValidation.Validate(sel).Should().NotBeNull();
    }

    [Fact]
    public void Validate_AllowsExistsWithoutValues()
    {
        var sel = new TagSelector(AnyOf: [new TagTerm("critical", TagOp.Exists)]);
        TagSelectorValidation.Validate(sel).Should().BeNull();
    }

    [Fact]
    public void ParseTags_SplitsKeyValueAndKeyOnly()
    {
        var map = TagSelectorValidation.ParseTags(["env:production", "critical", "url:https://x:8080"]);
        map["env"].Should().Be("production");
        map["critical"].Should().BeNull();
        map["url"].Should().Be("https://x:8080"); // only the first colon splits
    }
}
