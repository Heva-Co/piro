namespace Piro.Application.Models;

/// <summary>
/// Marker interface for a notification's content — <em>what</em> a notification is about, decoupled
/// from <em>how</em> it physically leaves (the dispatcher) and <em>when/to whom</em> it is routed (the
/// engine). It carries no rendering method: how a content type looks on a given channel lives on each
/// integration's own dispatcher (its own templates/rendering), not on the content. Used only as a
/// generic constraint on the notification dispatcher interfaces (RFC 0009 §4.1, §4.3).
/// </summary>
public interface INotificationContent
{
}
