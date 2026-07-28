using FluentAssertions;
using Piro.Application.Notifications;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

/// <summary>
/// Pins the numeric alignment between the domain's <see cref="AlertSeverity"/> and the contract-layer
/// <see cref="EventSeverity"/>.
/// </summary>
/// <remarks>
/// The two are deliberately separate types (RFC 0016) so an integration never references a Piro.Domain
/// enum, and <c>ToEventSeverity</c> is the supported way to cross between them. But they used to
/// disagree numerically — Critical was 1 in one and 2 in the other — so any code that copied the
/// integer instead of mapping it turned a Critical alert into a Warning.
///
/// That is not a cosmetic slip: a push built from a Warning is sent without
/// <c>interruption-level: critical</c>, so a critical page arrives silent and does not break through
/// Focus. It was found on a real device, where the alert landed but never made a sound.
/// </remarks>
public class EventSeverityAlignmentTests
{
    [Theory]
    [InlineData(AlertSeverity.Warning, EventSeverity.Warning)]
    [InlineData(AlertSeverity.Critical, EventSeverity.Critical)]
    public void TheTwoSeverityEnumsAgreeNumerically(AlertSeverity alert, EventSeverity evt)
    {
        // The point of the test: casting between them, which is exactly the mistake that caused the
        // bug, now lands on the matching member instead of a quieter one.
        ((int)alert).Should().Be((int)evt);
        ((EventSeverity)(int)alert).Should().Be(evt);
    }

    [Fact]
    public void InfoSitsOutsideTheSharedRange()
    {
        // Info has no AlertSeverity counterpart — it is for events that are not alerts (an incident
        // opening or resolving), so it must not collide with either shared value.
        ((int)EventSeverity.Info).Should().NotBe((int)AlertSeverity.Warning);
        ((int)EventSeverity.Info).Should().NotBe((int)AlertSeverity.Critical);
    }

    [Fact]
    public void EveryAlertSeverityMapsToTheSameNameOnTheOtherSide()
    {
        // Guards a member being added to one enum and forgotten in the other: a new AlertSeverity with
        // no EventSeverity twin would fall through the mapper and be delivered at the wrong urgency.
        foreach (var severity in Enum.GetValues<AlertSeverity>())
        {
            Enum.IsDefined(typeof(EventSeverity), severity.ToString())
                .Should().BeTrue($"AlertSeverity.{severity} has no EventSeverity counterpart");
        }
    }
}
