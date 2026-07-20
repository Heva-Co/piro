namespace Piro.Domain.Enums;

/// <summary>
/// Draft/finalized lifecycle of a postmortem report's <em>authoring</em> — NOT its visibility.
/// <para>
/// A postmortem is drafted, reviewed in a meeting, then finalized; <see cref="Published"/> means
/// "the review is finalized", and is entirely <b>internal-facing</b>. It does NOT expose the report on
/// the public status page (<c>apps/web</c>) — postmortems cannot be made public at all in the current
/// scope, and there is no route or field that surfaces them publicly.
/// </para>
/// <para>
/// <b>Do not reuse this enum for public visibility.</b> If public postmortems are ever added
/// (RFC 0005 §6, Phase 3b), they must carry a <em>separate</em> visibility flag — e.g. a
/// <c>PostmortemVisibility { Private, Public }</c> mirroring <see cref="IncidentVisibility"/> —
/// kept distinct from this internal Draft/Published status. Publishing to the public page also
/// requires redaction of internal root-cause detail, which is why it is deliberately out of scope here.
/// </para>
/// </summary>
public enum PostmortemStatus
{
    /// <summary>The review is still being authored/edited. Default on creation.</summary>
    Draft,

    /// <summary>The review is finalized. Internal-only — this is NOT public status-page visibility (see the type doc).</summary>
    Published
}
