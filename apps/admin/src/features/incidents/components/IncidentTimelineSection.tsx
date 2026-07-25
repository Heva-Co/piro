import { Globe, FlagTriangleRight } from "lucide-react";
import { marked } from "marked";
import { Marker, MarkerIcon, MarkerContent } from "@/components/ui/marker";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import type { IncidentTimelineEvent } from "@/lib/actions/incidents";
import { SYSTEM_EVENT_ICON, describeSystemEvent } from "./incidentTimeline";

interface Props {
  events: IncidentTimelineEvent[];
  hiddenCount: number;
  isResolved: boolean;
  onDeleteComment: (eventId: number) => void;
  onViewFull: () => void;
}

function IncidentTimelineSection(props: Props) {
  const { events, hiddenCount, isResolved, onDeleteComment, onViewFull } = props;
  const { formatTimestamp } = useFormattedDate();

  return (
    <div className="rounded-xl border border-border bg-card overflow-hidden">
      {events.length === 0 ? (
        <p className="px-5 py-8 text-center text-sm text-muted-foreground">No events yet.</p>
      ) : (
        <div>
          {events.map((e, i) => {
            const prev = events[i - 1];
            const needsTopBorder = i > 0 && prev.type === "CommentPosted" && e.type === "CommentPosted";

            if (e.type === "CommentPosted") {
              return (
                <div key={e.id} className={`px-5 py-4 flex gap-3 ${needsTopBorder ? "border-t border-border" : ""}`}>
                  <div className="flex-1 flex flex-col gap-1.5">
                    <div className="flex items-center gap-2 text-xs text-muted-foreground">
                      <span>{formatTimestamp(new Date(e.occurredAt).getTime() / 1000)}</span>
                      {e.visibility === "Public" && (
                        <span className="flex items-center gap-1 text-green-600 dark:text-green-400">
                          <Globe size={11} /> Public
                        </span>
                      )}
                    </div>
                    <div
                      className="text-sm prose prose-sm max-w-none"
                      dangerouslySetInnerHTML={{ __html: marked(e.comment ?? "", { async: false }) as string }}
                    />
                  </div>
                  {!isResolved && (
                    <button
                      onClick={() => onDeleteComment(e.id)}
                      className="shrink-0 rounded p-1 text-muted-foreground/40 hover:text-destructive hover:bg-destructive/10 transition-colors"
                    >
                      ×
                    </button>
                  )}
                </div>
              );
            }

            return (
              <div key={e.id} className="px-5 py-3">
                <Marker variant="separator">
                  <MarkerContent className="flex items-center gap-1.5">
                    <MarkerIcon>{SYSTEM_EVENT_ICON[e.type] ?? <FlagTriangleRight />}</MarkerIcon>
                    {describeSystemEvent(e)}
                    <span className="text-muted-foreground/70">· {formatTimestamp(new Date(e.occurredAt).getTime() / 1000)}</span>
                  </MarkerContent>
                </Marker>
              </div>
            );
          })}
        </div>
      )}
      {hiddenCount > 0 && (
        <button
          onClick={onViewFull}
          className="w-full px-5 py-3 text-center text-xs text-muted-foreground hover:text-foreground hover:bg-muted/30 transition-colors border-t border-border"
        >
          +{hiddenCount} more event{hiddenCount === 1 ? "" : "s"} — view full timeline
        </button>
      )}
    </div>
  );
}

export default IncidentTimelineSection;
