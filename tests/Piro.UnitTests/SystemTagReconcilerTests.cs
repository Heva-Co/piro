using FluentAssertions;
using NSubstitute;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

public class SystemTagReconcilerTests
{
    private readonly ITagRepository _tags = Substitute.For<ITagRepository>();
    private readonly SystemTagReconciler _sut;

    public SystemTagReconcilerTests()
    {
        _sut = new SystemTagReconciler(_tags);
    }

    [Fact]
    public async Task ReconcileCheck_WritesCheckTypeValueAndMultiRegionFlag()
    {
        var check = new Check { Id = 5, Type = CheckType.HTTP, IsMultiRegion = true };

        await _sut.ReconcileCheckAsync(check, default);

        await _tags.Received(1).SetCheckSystemTagAsync(5, "piro:check-type", "http", Arg.Any<CancellationToken>());
        await _tags.Received(1).SetCheckSystemTagAsync(5, "piro:multi-region", null, Arg.Any<CancellationToken>());
        await _tags.DidNotReceive().RemoveCheckSystemTagAsync(5, "piro:multi-region", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileCheck_SingleRegion_RemovesMultiRegionFlag()
    {
        var check = new Check { Id = 5, Type = CheckType.Heartbeat, IsMultiRegion = false };

        await _sut.ReconcileCheckAsync(check, default);

        await _tags.Received(1).SetCheckSystemTagAsync(5, "piro:check-type", "heartbeat", Arg.Any<CancellationToken>());
        await _tags.Received(1).RemoveCheckSystemTagAsync(5, "piro:multi-region", Arg.Any<CancellationToken>());
        await _tags.DidNotReceive().SetCheckSystemTagAsync(5, "piro:multi-region", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileWorker_WritesRegionAndFlags()
    {
        var id = Guid.NewGuid();
        var worker = new WorkerRegistration { Id = id, Region = "eu", IsBuiltIn = true, IsDefault = false };

        await _sut.ReconcileWorkerAsync(worker, default);

        await _tags.Received(1).SetWorkerSystemTagAsync(id, "piro:region", "eu", Arg.Any<CancellationToken>());
        await _tags.Received(1).SetWorkerSystemTagAsync(id, "piro:builtin", null, Arg.Any<CancellationToken>());
        await _tags.Received(1).RemoveWorkerSystemTagAsync(id, "piro:default", Arg.Any<CancellationToken>());
    }
}
