using System.Threading.Channels;
using FluentAssertions;
using NSubstitute;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.UnitTests;

/// <summary>Verifies the DAG cycle detection logic in <see cref="DependencyService"/>.</summary>
public class CycleDetectionTests
{
    private readonly IServiceRepository _serviceRepo = Substitute.For<IServiceRepository>();
    private readonly IServiceDependencyRepository _depRepo = Substitute.For<IServiceDependencyRepository>();
    private readonly DependencyService _sut;

    public CycleDetectionTests()
    {
        _sut = new DependencyService(_serviceRepo, _depRepo, Channel.CreateUnbounded<CheckStatusChangedEvent>());
    }

    [Fact]
    public async Task Add_LinearChain_Succeeds()
    {
        // C → D: adding should succeed since D has no existing deps
        SetupService(3, "c");
        SetupService(4, "d");

        _depRepo.ExistsAsync(3, 4, Arg.Any<CancellationToken>()).Returns(false);
        _depRepo.GetDependsOnIdsAsync(4, Arg.Any<DependencyPropagationMode?>(), Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.CreateAsync(Arg.Any<ServiceDependency>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<ServiceDependency>());

        var request = new AddDependencyRequest("d", DependencyPropagationMode.Blocking);
        var act = async () => await _sut.AddAsync("c", request);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Add_DirectSelfLoop_ThrowsValidation()
    {
        SetupService(1, "a");

        var request = new AddDependencyRequest("a", DependencyPropagationMode.Blocking);
        var act = async () => await _sut.AddAsync("a", request);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*itself*");
    }

    [Fact]
    public async Task Add_DirectCycle_ThrowsCyclicDependency()
    {
        // A depends on B already; trying to add B → A creates a cycle
        // Adding B→A: serviceId=2(b), candidateUpstream=1(a)
        // BFS from A: GetDependsOnIds(1) = [2] → finds 2 == serviceId → cycle!
        SetupService(1, "a");
        SetupService(2, "b");

        _depRepo.ExistsAsync(2, 1, Arg.Any<CancellationToken>()).Returns(false);
        _depRepo.GetDependsOnIdsAsync(1, Arg.Any<DependencyPropagationMode?>(), Arg.Any<CancellationToken>()).Returns([2]);
        _depRepo.GetDependsOnIdsAsync(2, Arg.Any<DependencyPropagationMode?>(), Arg.Any<CancellationToken>()).Returns([]);

        var request = new AddDependencyRequest("a", DependencyPropagationMode.Blocking);
        var act = async () => await _sut.AddAsync("b", request);

        await act.Should().ThrowAsync<CyclicDependencyException>();
    }

    [Fact]
    public async Task Add_DiamondDependency_Succeeds()
    {
        // A → B, A → C (already), B → D, C → D (diamond — no cycle)
        // Adding A → C: serviceId=1(a), candidateUpstream=3(c)
        // BFS from C: C→D, D→nothing — never reaches A
        SetupService(1, "a");
        SetupService(3, "c");
        SetupService(4, "d");

        _depRepo.ExistsAsync(1, 3, Arg.Any<CancellationToken>()).Returns(false);
        _depRepo.GetDependsOnIdsAsync(3, Arg.Any<DependencyPropagationMode?>(), Arg.Any<CancellationToken>()).Returns([4]);
        _depRepo.GetDependsOnIdsAsync(4, Arg.Any<DependencyPropagationMode?>(), Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.CreateAsync(Arg.Any<ServiceDependency>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<ServiceDependency>());

        var request = new AddDependencyRequest("c", DependencyPropagationMode.Blocking);
        var act = async () => await _sut.AddAsync("a", request);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Add_TransitiveCycle_ThrowsCyclicDependency()
    {
        // A→B→C already; trying to add C→A creates a transitive cycle
        // Adding C→A: serviceId=3(c), candidateUpstream=1(a)
        // BFS from A: A→B→C → finds 3==serviceId → cycle!
        SetupService(1, "a");
        SetupService(2, "b");
        SetupService(3, "c");

        _depRepo.ExistsAsync(3, 1, Arg.Any<CancellationToken>()).Returns(false);
        _depRepo.GetDependsOnIdsAsync(1, Arg.Any<DependencyPropagationMode?>(), Arg.Any<CancellationToken>()).Returns([2]);
        _depRepo.GetDependsOnIdsAsync(2, Arg.Any<DependencyPropagationMode?>(), Arg.Any<CancellationToken>()).Returns([3]);
        _depRepo.GetDependsOnIdsAsync(3, Arg.Any<DependencyPropagationMode?>(), Arg.Any<CancellationToken>()).Returns([]);

        var request = new AddDependencyRequest("a", DependencyPropagationMode.Blocking);
        var act = async () => await _sut.AddAsync("c", request);

        await act.Should().ThrowAsync<CyclicDependencyException>();
    }

    [Fact]
    public async Task Add_DuplicateEdge_ThrowsValidation()
    {
        SetupService(1, "a");
        SetupService(2, "b");

        _depRepo.ExistsAsync(1, 2, Arg.Any<CancellationToken>()).Returns(true);

        var request = new AddDependencyRequest("b", DependencyPropagationMode.Blocking);
        var act = async () => await _sut.AddAsync("a", request);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*already depends on*");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void SetupService(int id, string slug)
    {
        var service = new Service { Id = id, Slug = slug };
        _serviceRepo.GetBySlugAsync(slug, Arg.Any<CancellationToken>()).Returns(service);
        _serviceRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(service);
    }
}
