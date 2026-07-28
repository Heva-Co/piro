namespace Piro.Domain.Auditing;

/// <summary>
/// Opt-in marker: changes to this entity are recorded in the audit trail (issue #17).
/// </summary>
/// <remarks>
/// The audit interceptor ignores every entity that does not implement this interface, so the
/// failure mode of forgetting the marker is "a change went unrecorded" rather than "the audit
/// table filled up with machine noise". That trade-off is deliberate: high-volume, machine-written
/// tables (<c>CheckDataPoint</c>, <c>Alert</c>, the delivery logs) must never be marked, and an
/// inverted opt-out marker would silently capture them the moment someone adds a new entity.
/// <para>
/// Only entities a human edits through the admin belong here. The interceptor additionally skips
/// any batch with no authenticated user, so background jobs touching a marked entity write nothing.
/// </para>
/// <para>
/// Marking an entity captures a JSON snapshot of its scalar properties. Secrets must be kept out
/// with <see cref="NotAuditedAttribute"/>, or by not marking the owning entity at all.
/// </para>
/// </remarks>
public interface IAuditable;
