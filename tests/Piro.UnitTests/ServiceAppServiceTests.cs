using FluentAssertions;
using NSubstitute;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;

namespace Piro.UnitTests;

/// <summary>
/// Covers the two ServiceAppService correctness fixes from RFC 0019 §4.4 and §4.5: the tri-state
/// escalation-policy patch (omitted leaves it, explicit null clears it) and unscheduling a deleted
/// service's checks so their Quartz jobs do not outlive the rows.
/// </summary>
public class ServiceAppServiceTests
{
    private readonly IServiceRepository _services = Substitute.For<IServiceRepository>();
    private readonly IEscalationPolicyRepository _policies = Substitute.For<IEscalationPolicyRepository>();
    private readonly ICheckRepository _checks = Substitute.For<ICheckRepository>();
    private readonly ICheckSchedulerService _scheduler = Substitute.For<ICheckSchedulerService>();
    private readonly ServiceAppService _sut;

    public ServiceAppServiceTests()
    {
        _sut = new ServiceAppService(_services, _policies, _checks, _scheduler);
    }

    private Service GivenService(int? escalationPolicyId = null)
    {
        var service = new Service { Id = 1, Slug = "svc", Name = "Svc", EscalationPolicyId = escalationPolicyId };
        _services.GetBySlugAsync("svc", Arg.Any<CancellationToken>()).Returns(service);
        _services.UpdateAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<Service>());
        return service;
    }

    private static UpdateServiceRequest Update(Patch<int?>? escalationPolicyId) =>
        new("Renamed", null, null, null, null, escalationPolicyId);

    [Fact]
    public async Task OmittedEscalationPolicy_LeavesItUnchanged()
    {
        // The regression this guards: a partial update used to null the policy, silently disabling
        // on-call notifications for the service.
        var service = GivenService(escalationPolicyId: 7);

        await _sut.UpdateAsync("svc", Update(escalationPolicyId: null));

        service.EscalationPolicyId.Should().Be(7);
        service.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task ExplicitNullEscalationPolicy_ClearsIt()
    {
        var service = GivenService(escalationPolicyId: 7);

        await _sut.UpdateAsync("svc", Update(new Patch<int?>(null)));

        service.EscalationPolicyId.Should().BeNull();
    }

    [Fact]
    public async Task ExplicitEscalationPolicy_IsSetAfterValidation()
    {
        var service = GivenService();
        _policies.GetByIdAsync(3, Arg.Any<CancellationToken>())
            .Returns(new EscalationPolicy { Id = 3, Name = "Primary" });

        await _sut.UpdateAsync("svc", Update(new Patch<int?>(3)));

        service.EscalationPolicyId.Should().Be(3);
    }

    [Fact]
    public async Task Delete_UnschedulesEveryCheckOfTheService()
    {
        var service = GivenService();
        _checks.GetByServiceIdAsync(1, Arg.Any<CancellationToken>()).Returns(
        [
            new Check { Id = 10, ServiceId = 1, Slug = "a", Name = "A" },
            new Check { Id = 11, ServiceId = 1, Slug = "b", Name = "B" },
        ]);

        await _sut.DeleteAsync("svc");

        await _services.Received(1).DeleteAsync(service, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).UnscheduleAsync(10, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).UnscheduleAsync(11, Arg.Any<CancellationToken>());
    }
}
