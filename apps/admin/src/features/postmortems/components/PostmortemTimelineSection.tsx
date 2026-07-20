import { useFormattedDate } from "@/hooks/useFormattedDate";
import type { Postmortem } from "@/lib/actions/postmortems";

interface Props {
  postmortem: Postmortem;
}

// The report's derived timeline: incident events / impact changes / alerts from every linked
// incident, merged chronologically. Read-only in this phase (author annotations come later).
function PostmortemTimelineSection(props: Props) {
  const { postmortem } = props;
  const { formatDateTime } = useFormattedDate();

  return (
    <div className="rounded-xl border bg-card">
      <div className="border-b px-5 py-3">
        <h2 className="text-sm font-semibold">Timeline</h2>
        <p className="text-xs text-muted-foreground mt-0.5">
          Derived from the referenced incidents.
        </p>
      </div>

      <div className="p-5">
        {postmortem.timeline.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Link an incident to see its timeline here.
          </p>
        ) : (
          <ol className="flex flex-col gap-3">
            {postmortem.timeline.map((item, i) => (
              <li key={i} className="flex gap-3 text-sm">
                <span className="w-40 shrink-0 text-xs text-muted-foreground">
                  {formatDateTime(item.occurredAt)}
                </span>
                <div className="flex flex-col">
                  <span className="text-xs font-medium text-muted-foreground">
                    {item.source} · #{item.incidentId} {item.incidentTitle}
                  </span>
                  {item.text && <span>{item.text}</span>}
                  {(item.oldStatus || item.newStatus) && (
                    <span className="text-muted-foreground">
                      {item.oldStatus ?? "—"} → {item.newStatus ?? "—"}
                    </span>
                  )}
                </div>
              </li>
            ))}
          </ol>
        )}
      </div>
    </div>
  );
}

export default PostmortemTimelineSection;
