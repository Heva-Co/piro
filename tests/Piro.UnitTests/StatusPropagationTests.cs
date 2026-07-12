using FluentAssertions;
using NSubstitute;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

/// <summary>Verifies the status propagation algorithm in <see cref="ServiceStatusService"/>.</summary>
public class StatusPropagationTests
{
    private readonly IServiceRepository _serviceRepo = Substitute.For<IServiceRepository>();
    private readonly ICheckRepository _checkRepo = Substitute.For<ICheckRepository>();
    private readonly IServiceDependencyRepository _depRepo = Substitute.For<IServiceDependencyRepository>();
    private readonly IIncidentRepository _incidentRepo = Substitute.For<IIncidentRepository>();
    private readonly IMaintenanceRepository _maintenanceRepo = Substitute.For<IMaintenanceRepository>();
    private readonly ServiceStatusService _sut;

    public StatusPropagationTests()
    {
        _incidentRepo.GetActiveImpactForServiceAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((ServiceStatus?)null);
        _maintenanceRepo.HasActiveWindowAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _sut = new ServiceStatusService(_serviceRepo, _checkRepo, _depRepo, _incidentRepo, _maintenanceRepo);
    }

    // ── Single service, no deps ─────────────────────────────────────────────

    [Fact]
    public async Task NoChecks_ReturnsNoData()
    {
        SetupService(1, "svc", ServiceStatus.NO_DATA);
        _checkRepo.GetByServiceIdAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.GetUpstreamDependenciesAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.NO_DATA), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AllChecksUp_ReturnsUp()
    {
        SetupService(1, "svc", ServiceStatus.NO_DATA);
        SetupChecks(1, [ServiceStatus.UP, ServiceStatus.UP]);
        _depRepo.GetUpstreamDependenciesAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.UP), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OneCheckDown_ReturnsDown()
    {
        SetupService(1, "svc", ServiceStatus.NO_DATA);
        SetupChecks(1, [ServiceStatus.UP, ServiceStatus.DOWN]);
        _depRepo.GetUpstreamDependenciesAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.DOWN), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MixedDegradedDown_ReturnsDown()
    {
        SetupService(1, "svc", ServiceStatus.NO_DATA);
        SetupChecks(1, [ServiceStatus.DEGRADED, ServiceStatus.DOWN, ServiceStatus.UP]);
        _depRepo.GetUpstreamDependenciesAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.DOWN), Arg.Any<CancellationToken>());
    }

    // ── Blocking dependency propagation ─────────────────────────────────────

    [Fact]
    public async Task BlockingUpstreamDown_PropagatesDown()
    {
        // svc (UP checks) + blocking dep on upstream (DOWN) → svc should be DOWN
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.UP]);
        SetupUpstreamDep(1, upstream: MakeService(2, "upstream", ServiceStatus.DOWN), DependencyPropagationMode.Blocking);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.DOWN), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BlockingUpstreamDegraded_PropagatesDegraded()
    {
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.UP]);
        SetupUpstreamDep(1, upstream: MakeService(2, "upstream", ServiceStatus.DEGRADED), DependencyPropagationMode.Blocking);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.DEGRADED), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BlockingUpstreamUp_DoesNotPropagate()
    {
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.UP]);
        SetupUpstreamDep(1, upstream: MakeService(2, "upstream", ServiceStatus.UP), DependencyPropagationMode.Blocking);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.UP), Arg.Any<CancellationToken>());
    }

    // ── SoftBlocking dependency propagation ─────────────────────────────────

    [Fact]
    public async Task SoftBlockingUpstreamDown_CapsAtDegraded()
    {
        // Upstream is DOWN but mode is SoftBlocking → svc should be DEGRADED, not DOWN
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.UP]);
        SetupUpstreamDep(1, upstream: MakeService(2, "upstream", ServiceStatus.DOWN), DependencyPropagationMode.SoftBlocking);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.DEGRADED), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SoftBlockingUpstreamDegraded_PropagatesDegraded()
    {
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.UP]);
        SetupUpstreamDep(1, upstream: MakeService(2, "upstream", ServiceStatus.DEGRADED), DependencyPropagationMode.SoftBlocking);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.DEGRADED), Arg.Any<CancellationToken>());
    }

    // ── Advisory dependency ──────────────────────────────────────────────────

    [Fact]
    public async Task AdvisoryUpstreamDown_DoesNotPropagate()
    {
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.UP]);
        // Advisory deps are not returned by GetUpstreamDependenciesAsync (filtered in repo)
        _depRepo.GetUpstreamDependenciesAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.UP), Arg.Any<CancellationToken>());
    }

    // ── Cascade ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusChanges_ReturnsDownstreamIds()
    {
        // svc changes from UP to DOWN → should return downstream IDs
        SetupService(1, "svc", ServiceStatus.UP);   // was UP
        SetupChecks(1, [ServiceStatus.DOWN]);        // now DOWN
        _depRepo.GetUpstreamDependenciesAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([10, 11]);

        var downstream = await _sut.ComputeAsync(1);

        downstream.Should().BeEquivalentTo([10, 11]);
    }

    [Fact]
    public async Task StatusUnchanged_ReturnsEmptyDownstream()
    {
        // svc already DOWN, check is DOWN → no change → empty cascade
        SetupService(1, "svc", ServiceStatus.DOWN);
        SetupChecks(1, [ServiceStatus.DOWN]);
        _depRepo.GetUpstreamDependenciesAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        var downstream = await _sut.ComputeAsync(1);

        downstream.Should().BeEmpty();
        await _depRepo.DidNotReceive().GetBlockingDownstreamServiceIdsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // ── Diamond dependency (worst-of-multiple-upstreams) ────────────────────

    [Fact]
    public async Task MultipleUpstreams_WorstWins()
    {
        // svc depends on upstreamA (DEGRADED, Blocking) and upstreamB (DOWN, Blocking) → DOWN
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.UP]);

        var deps = new[]
        {
            MakeDep(serviceId: 1, upstream: MakeService(2, "a", ServiceStatus.DEGRADED), DependencyPropagationMode.Blocking),
            MakeDep(serviceId: 1, upstream: MakeService(3, "b", ServiceStatus.DOWN),     DependencyPropagationMode.Blocking)
        };
        _depRepo.GetUpstreamDependenciesAsync(1, Arg.Any<CancellationToken>()).Returns(deps);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.DOWN), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OwnCheckDownBeatsBlockingDegraded_ReturnsDown()
    {
        // Own check is DOWN, upstream is DEGRADED (Blocking) → DOWN wins
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.DOWN]);
        SetupUpstreamDep(1, upstream: MakeService(2, "upstream", ServiceStatus.DEGRADED), DependencyPropagationMode.Blocking);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.DOWN), Arg.Any<CancellationToken>());
    }

    // ── Maintenance override ─────────────────────────────────────────────────

    [Fact]
    public async Task ActiveMaintenanceWindow_OverridesCheckStatus()
    {
        SetupService(1, "svc", ServiceStatus.UP);
        SetupChecks(1, [ServiceStatus.UP]);
        _maintenanceRepo.HasActiveWindowAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        _depRepo.GetBlockingDownstreamServiceIdsAsync(1, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ComputeAsync(1);

        await _serviceRepo.Received(1).UpdateAsync(Arg.Is<Service>(s => s.CurrentStatus == ServiceStatus.MAINTENANCE), Arg.Any<CancellationToken>());
    }

    // ── Service not found ────────────────────────────────────────────────────

    [Fact]
    public async Task ServiceNotFound_ReturnsEmpty()
    {
        _serviceRepo.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Service?)null);

        var downstream = await _sut.ComputeAsync(999);

        downstream.Should().BeEmpty();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void SetupService(int id, string slug, ServiceStatus currentStatus)
    {
        var service = MakeService(id, slug, currentStatus);
        _serviceRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(service);
        _serviceRepo.GetBySlugAsync(slug, Arg.Any<CancellationToken>()).Returns(service);
        _serviceRepo.UpdateAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Service>());
    }

    private void SetupChecks(int serviceId, IEnumerable<ServiceStatus> statuses)
    {
        var checks = statuses.Select((s, i) => new Check
        {
            Id = i + 1, ServiceId = serviceId, Slug = $"check-{i}", Name = $"Check {i}",
            IsActive = true, CurrentStatus = s
        }).ToList();
        _checkRepo.GetByServiceIdAsync(serviceId, Arg.Any<CancellationToken>()).Returns(checks);
    }

    private void SetupUpstreamDep(int serviceId, Service upstream, DependencyPropagationMode mode)
    {
        var dep = MakeDep(serviceId, upstream, mode);
        _depRepo.GetUpstreamDependenciesAsync(serviceId, Arg.Any<CancellationToken>()).Returns([dep]);
    }

    private static Service MakeService(int id, string slug, ServiceStatus status) =>
        new() { Id = id, Slug = slug, CurrentStatus = status };

    private static ServiceDependency MakeDep(int serviceId, Service upstream, DependencyPropagationMode mode) =>
        new() { ServiceId = serviceId, DependsOnServiceId = upstream.Id, PropagationMode = mode, DependsOnService = upstream };
}
