using FluentAssertions;
using NSubstitute;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Workers;

namespace Piro.UnitTests;

/// <summary>
/// Routing decisions for RFC 0008 Part B tag-based scheduling: no required tags fans out to all live
/// workers; required tags dispatch only to the matching live subset; an empty live match is a transient
/// MONITOR_OUTAGE when a registered worker could match, otherwise a permanent UNSCHEDULABLE (§4.6).
/// </summary>
public class RoutingCheckJobDispatcherTests
{
    private readonly IWorkerFanoutDispatcher _fanout = Substitute.For<IWorkerFanoutDispatcher>();
    private readonly IWorkerRegistry _registry = Substitute.For<IWorkerRegistry>();
    private readonly ITagRepository _tags = Substitute.For<ITagRepository>();
    private readonly RoutingCheckJobDispatcher _sut;

    public RoutingCheckJobDispatcherTests()
    {
        _sut = new RoutingCheckJobDispatcher(_fanout, _registry, _tags);
    }

    private static WorkerInfo Worker(string connId, string region, bool inProcess = false, params (string Key, string? Value)[] tags)
    {
        var now = DateTime.UtcNow;
        return new WorkerInfo(Guid.NewGuid(), connId, region, now, now)
        {
            IsInProcess = inProcess,
            Tags = tags.ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal),
        };
    }

    private static CheckRequiredWorkerTag Required(string key, string? value = null) =>
        new() { TagId = 1, Value = value, Tag = new Tag { Key = key, Source = TagSource.System } };

    private void RequiredTags(int checkId, params CheckRequiredWorkerTag[] rows) =>
        _tags.GetRequiredWorkerTagsAsync(checkId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CheckRequiredWorkerTag>>(rows));

    private void LiveWorkers(params WorkerInfo[] workers) =>
        _registry.GetAll().Returns(workers.ToList());

    private void RegisteredTagSets(params IReadOnlyDictionary<string, string?>[] sets) =>
        _tags.GetAllWorkerTagSetsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>(sets.ToList()));

    [Fact]
    public async Task NoRequiredTags_FansOutToAllLiveWorkers()
    {
        var check = new Check { Id = 1 };
        RequiredTags(1); // empty
        var builtin = Worker("__api_worker__", "default", inProcess: true);
        var remote = Worker("conn-eu", "eu");
        LiveWorkers(builtin, remote);

        await _sut.DispatchAsync(check);

        await _fanout.Received(1).DispatchToWorkersAsync(
            check,
            Arg.Is<IReadOnlyList<WorkerInfo>>(w => w.Count == 2 && w.Contains(builtin) && w.Contains(remote)),
            Arg.Any<CancellationToken>());
        await _fanout.DidNotReceive().RecordUnschedulableAsync(Arg.Any<Check>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoRequiredTags_SingleNode_FansOutToTheBuiltinOnly()
    {
        var check = new Check { Id = 1 };
        RequiredTags(1);
        var builtin = Worker("__api_worker__", "default", inProcess: true);
        LiveWorkers(builtin); // single-node: only the built-in

        await _sut.DispatchAsync(check);

        await _fanout.Received(1).DispatchToWorkersAsync(
            check,
            Arg.Is<IReadOnlyList<WorkerInfo>>(w => w.Count == 1 && w[0] == builtin),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequiredTags_DispatchesOnlyToTheMatchingLiveSubset()
    {
        var check = new Check { Id = 2 };
        RequiredTags(2, Required("piro:region", "eu"));
        var eu = Worker("conn-eu", "eu", tags: ("piro:region", "eu"));
        var us = Worker("conn-us", "us", tags: ("piro:region", "us"));
        LiveWorkers(eu, us);

        await _sut.DispatchAsync(check);

        await _fanout.Received(1).DispatchToWorkersAsync(
            check,
            Arg.Is<IReadOnlyList<WorkerInfo>>(w => w.Count == 1 && w[0] == eu),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequiredTags_NoLiveMatchButRegisteredMatch_RecordsMonitorOutage()
    {
        var check = new Check { Id = 3 };
        RequiredTags(3, Required("piro:region", "eu"));
        LiveWorkers(Worker("conn-us", "us", tags: ("piro:region", "us"))); // live, but not eu
        RegisteredTagSets(new Dictionary<string, string?>(StringComparer.Ordinal) { ["piro:region"] = "eu" });

        await _sut.DispatchAsync(check);

        await _fanout.Received(1).RecordMonitorOutageAsync(check, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fanout.DidNotReceive().RecordUnschedulableAsync(Arg.Any<Check>(), Arg.Any<CancellationToken>());
        await _fanout.DidNotReceive().DispatchToWorkersAsync(Arg.Any<Check>(), Arg.Any<IReadOnlyList<WorkerInfo>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequiredTags_NoLiveAndNoRegisteredMatch_RecordsUnschedulable()
    {
        var check = new Check { Id = 4 };
        RequiredTags(4, Required("piro:region", "eu"));
        LiveWorkers(Worker("conn-us", "us", tags: ("piro:region", "us")));
        RegisteredTagSets(new Dictionary<string, string?>(StringComparer.Ordinal) { ["piro:region"] = "us" }); // no eu anywhere

        await _sut.DispatchAsync(check);

        await _fanout.Received(1).RecordUnschedulableAsync(check, Arg.Any<CancellationToken>());
        await _fanout.DidNotReceive().RecordMonitorOutageAsync(Arg.Any<Check>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequiredKeyOnly_MatchesWorkerCarryingThatKeyAnyValue()
    {
        var check = new Check { Id = 5 };
        RequiredTags(5, Required("gpu")); // key-only requirement
        var gpu = Worker("conn-gpu", "eu", tags: ("gpu", "a100"));
        var plain = Worker("conn-plain", "eu", tags: ("piro:region", "eu"));
        LiveWorkers(gpu, plain);

        await _sut.DispatchAsync(check);

        await _fanout.Received(1).DispatchToWorkersAsync(
            check,
            Arg.Is<IReadOnlyList<WorkerInfo>>(w => w.Count == 1 && w[0] == gpu),
            Arg.Any<CancellationToken>());
    }
}
