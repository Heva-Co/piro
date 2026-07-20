import { useState } from "react";
import {
  AlertTriangle,
  CircleDot,
  MessageSquareText,
  Pencil,
  Plus,
  Trash2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import Timeline from "@/components/timeline/Timeline";
import TimelineRow from "@/components/timeline/TimelineRow";
import { timelineSourceLabel } from "@/components/timeline/timelineLabels";
import PostmortemAnnotationForm from "@/features/postmortems/components/PostmortemAnnotationForm";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import type { Postmortem, PostmortemTimelineItem } from "@/lib/actions/postmortems";

interface Props {
  postmortem: Postmortem;
  saving: boolean;
  onAdd: (occurredAt: string, body: string) => void;
  onEdit: (entryId: number, occurredAt: string, body: string) => void;
  onDelete: (entryId: number) => void;
}

// Picks an icon per timeline item: annotations, alert events, and generic incident events each read
// differently, mirroring the incident timeline's visual vocabulary.
function itemIcon(item: PostmortemTimelineItem) {
  if (item.isAnnotation) return <MessageSquareText />;
  if (item.source.startsWith("alert:")) return <AlertTriangle />;
  return <CircleDot />;
}

// The report's timeline: incident events / impact changes / alerts from every linked incident (derived,
// read-only), merged chronologically with the author's own annotations (add/edit/delete-able here).
// Renders through the shared Timeline/TimelineRow so it reads as one system with the incident timeline.
function PostmortemTimelineSection(props: Props) {
  const { postmortem, saving, onAdd, onEdit, onDelete } = props;
  const { formatDateTime } = useFormattedDate();
  const [adding, setAdding] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);

  function handleAdd(occurredAt: string, body: string) {
    onAdd(occurredAt, body);
    setAdding(false);
  }

  function handleEdit(entryId: number, occurredAt: string, body: string) {
    onEdit(entryId, occurredAt, body);
    setEditingId(null);
  }

  return (
    <div className="rounded-xl border bg-card">
      <div className="flex items-center justify-between border-b px-5 py-3">
        <div>
          <h2 className="text-sm font-semibold">Timeline</h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            Derived from the referenced incidents, plus your annotations.
          </p>
        </div>
        {!adding && (
          <Button size="sm" variant="outline" onClick={() => setAdding(true)}>
            <Plus size={13} /> Add note
          </Button>
        )}
      </div>

      <div className="flex flex-col gap-4 p-5">
        {adding && (
          <PostmortemAnnotationForm
            saving={saving}
            submitLabel="Add note"
            onSubmit={handleAdd}
            onCancel={() => setAdding(false)}
          />
        )}

        {postmortem.timeline.length === 0 && !adding ? (
          <p className="text-sm text-muted-foreground">
            Link an incident or add a note to build the timeline.
          </p>
        ) : (
          <Timeline>
            {postmortem.timeline.map((item) => {
              const isEditing = item.isAnnotation && editingId === item.entryId;
              if (isEditing) {
                return (
                  <PostmortemAnnotationForm
                    key={`edit-${item.entryId}`}
                    initialOccurredAt={item.occurredAt}
                    initialBody={item.text ?? ""}
                    saving={saving}
                    submitLabel="Save note"
                    onSubmit={(occurredAt, body) => handleEdit(item.entryId!, occurredAt, body)}
                    onCancel={() => setEditingId(null)}
                  />
                );
              }

              const meta = item.isAnnotation
                ? `${formatDateTime(item.occurredAt)} · Note${item.actorName ? ` · ${item.actorName}` : ""}`
                : `${formatDateTime(item.occurredAt)} · ${timelineSourceLabel(item.source)} · #${item.incidentId} ${item.incidentTitle}`;

              return (
                <TimelineRow
                  key={item.isAnnotation ? `a-${item.entryId}` : `d-${item.source}-${item.occurredAt}`}
                  icon={itemIcon(item)}
                  meta={meta}
                  actions={
                    item.isAnnotation ? (
                      <>
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          onClick={() => setEditingId(item.entryId!)}
                          aria-label="Edit note"
                        >
                          <Pencil size={13} />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          onClick={() => onDelete(item.entryId!)}
                          aria-label="Delete note"
                        >
                          <Trash2 size={13} />
                        </Button>
                      </>
                    ) : undefined
                  }
                >
                  {item.text && <span>{item.text}</span>}
                  {(item.oldStatus || item.newStatus) && (
                    <span className="text-muted-foreground">
                      {item.oldStatus ?? "—"} → {item.newStatus ?? "—"}
                    </span>
                  )}
                </TimelineRow>
              );
            })}
          </Timeline>
        )}
      </div>
    </div>
  );
}

export default PostmortemTimelineSection;
