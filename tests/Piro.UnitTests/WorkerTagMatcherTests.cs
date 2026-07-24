using FluentAssertions;
using Piro.Domain.Tags;

namespace Piro.UnitTests;

public class WorkerTagMatcherTests
{
    private static IReadOnlyDictionary<string, string?> WorkerTags(params (string Key, string? Value)[] pairs)
    {
        var d = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    [Fact]
    public void EmptyRequirement_MatchesEveryWorker()
    {
        WorkerTagMatcher.IsEligible([], WorkerTags(("piro:region", "eu"))).Should().BeTrue();
        WorkerTagMatcher.IsEligible([], WorkerTags()).Should().BeTrue();
    }

    [Fact]
    public void ValuedRequirement_MatchesExactValue()
    {
        RequiredWorkerTag[] req = [new("piro:region", "eu")];
        WorkerTagMatcher.IsEligible(req, WorkerTags(("piro:region", "eu"))).Should().BeTrue();
        WorkerTagMatcher.IsEligible(req, WorkerTags(("piro:region", "us"))).Should().BeFalse();
        WorkerTagMatcher.IsEligible(req, WorkerTags()).Should().BeFalse();
    }

    [Fact]
    public void KeyOnlyRequirement_MatchesAnyValueForThatKey()
    {
        RequiredWorkerTag[] req = [new("gpu", null)];
        WorkerTagMatcher.IsEligible(req, WorkerTags(("gpu", "a100"))).Should().BeTrue();
        WorkerTagMatcher.IsEligible(req, WorkerTags(("gpu", null))).Should().BeTrue();
        WorkerTagMatcher.IsEligible(req, WorkerTags(("cpu", "x"))).Should().BeFalse();
    }

    [Fact]
    public void MultipleRequirements_IntersectionIsAtLeastOne()
    {
        // §4.5: the check runs where they intersect — sharing at least one required pair is enough.
        RequiredWorkerTag[] req = [new("piro:region", "eu"), new("piro:region", "us")];
        WorkerTagMatcher.IsEligible(req, WorkerTags(("piro:region", "us"))).Should().BeTrue();
        WorkerTagMatcher.IsEligible(req, WorkerTags(("piro:region", "asia"))).Should().BeFalse();
    }

    [Fact]
    public void RequirementForKeyWorkerLacks_DoesNotMatch()
    {
        RequiredWorkerTag[] req = [new("compliance", "hipaa")];
        WorkerTagMatcher.IsEligible(req, WorkerTags(("piro:region", "eu"))).Should().BeFalse();
    }
}
