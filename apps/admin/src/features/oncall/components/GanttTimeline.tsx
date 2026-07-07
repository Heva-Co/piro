import { useState } from "react";
import { X, Pencil, Trash2 } from "lucide-react";
import type { OnCallSlot, OnCallLayer } from "@/lib/api";
import { getWeekday } from "@/utils/date";

interface OverrideInfo {
  fromInitials: string;
  fromColor: string;
  toInitials: string;
  toColor: string;
}

interface GanttRow {
  label: string;
  slots: OnCallSlot[];
  layer?: OnCallLayer;
  overrideInfo?: OverrideInfo;
}

interface GanttTimelineProps {
  rows: GanttRow[];
  from: Date;
  to: Date;
  totalMs: number;
  onDeleteSlot?: (slot: OnCallSlot) => void;
  onEditLayer?: (layer: OnCallLayer) => void;
  onDeleteLayer?: (layerId: number) => void;
}

function fmtDay(date: Date): string {
  return date.toLocaleDateString(undefined, { month: "numeric", day: "numeric", timeZone: "UTC" }).replace(",", "");
}


function fmtDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    month: "short", day: "numeric", year: "numeric",
    hour: "numeric", minute: "2-digit",
  });
}

function getDayColumns(from: Date, to: Date): Date[] {
  const days: Date[] = [];
  let ms = Date.UTC(from.getUTCFullYear(), from.getUTCMonth(), from.getUTCDate());
  while (ms < to.getTime()) {
    days.push(new Date(ms));
    ms += 86_400_000;
  }
  return days;
}

export function GanttTimeline({ rows, from, to, onDeleteSlot, onEditLayer, onDeleteLayer }: GanttTimelineProps) {
  const [modal, setModal] = useState<OnCallSlot | null>(null);

  const totalMs = to.getTime() - from.getTime();
  const dayColumns = getDayColumns(from, to);

  function pct(ms: number): string {
    return `${((ms / totalMs) * 100).toFixed(4)}%`;
  }

  function slotLeft(slot: OnCallSlot): string {
    const start = Math.max(new Date(slot.startsAt).getTime(), from.getTime());
    return pct(start - from.getTime());
  }

  function slotWidth(slot: OnCallSlot): string {
    const start = Math.max(new Date(slot.startsAt).getTime(), from.getTime());
    // Clip 1ms before `to` so the last slot never overflows the track
    const end = Math.min(new Date(slot.endsAt).getTime(), to.getTime() - 1);
    return pct(Math.max(0, end - start));
  }

  if (rows.length === 0) {
    return <div className="py-4 text-sm text-muted-foreground italic">No rotations configured.</div>;
  }

  const MIN_TRACK_PX = 600;

  // Label column: fixed width, sticky
  const LABEL_W = 144;

  return (
    <>
      {/* Outer: flex row. Left col is fixed; right col scrolls horizontally. */}
      <div className="flex">
        {/* Fixed label column */}
        <div className="shrink-0 bg-card z-10" style={{ width: LABEL_W }}>
          {/* Header spacer matches the h-9 header in the scroll pane */}
          <div className="h-9 border-b border-border" />
          {rows.map((row, ri) => (
            <div key={ri} className="group pl-4 pr-2 pt-1 pb-1" style={{ minHeight: "2.25rem" }}>
              <div className="flex items-center gap-1">
                {row.overrideInfo ? (
                  <div className="flex items-center gap-1 flex-1 min-w-0">
                    <span className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0" style={{ backgroundColor: row.overrideInfo.fromColor }}>
                      {row.overrideInfo.fromInitials}
                    </span>
                    <span className="text-[10px] text-muted-foreground shrink-0">→</span>
                    <span className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0" style={{ backgroundColor: row.overrideInfo.toColor }}>
                      {row.overrideInfo.toInitials}
                    </span>
                  </div>
                ) : (
                  <span className="text-xs font-semibold text-foreground truncate flex-1" title={row.label}>{row.label}</span>
                )}
                {row.layer && onEditLayer && (
                  <button onClick={() => onEditLayer(row.layer!)} className="p-0.5 rounded text-muted-foreground hover:text-foreground hover:bg-muted transition-colors shrink-0" title="Edit layer">
                    <Pencil size={10} />
                  </button>
                )}
                {row.layer && onDeleteLayer && (
                  <button onClick={() => onDeleteLayer(row.layer!.id)} className="p-0.5 rounded text-muted-foreground hover:text-destructive hover:bg-destructive/10 transition-colors shrink-0" title="Delete layer">
                    <Trash2 size={10} />
                  </button>
                )}
              </div>
              {row.layer && row.layer.users.length > 0 && (
                <div className="flex items-center gap-0.5 mt-0.5 flex-wrap">
                  {row.layer.users.map((u) => (
                    <span key={u.id} title={u.userName} className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0 cursor-default" style={{ backgroundColor: u.userColor || "#6366f1" }}>
                      {u.userInitials}
                    </span>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>

        {/* Scrollable tracks column */}
        <div className="flex-1 overflow-x-auto pb-1">
          {/* Day header */}
          <div className="relative h-9 border-b border-border" style={{ minWidth: MIN_TRACK_PX }}>
            {dayColumns.map((day, i) => (
              <div
                key={i}
                className="absolute top-0 h-full flex flex-col items-center justify-center text-xs text-muted-foreground border-l border-border/50"
                style={{ left: pct(day.getTime() - from.getTime()), width: pct(86_400_000) }}
              >
                <span className="font-medium leading-tight">{getWeekday(day)}</span>
                <span className="leading-tight">{fmtDay(day)}</span>
              </div>
            ))}
          </div>

          {/* Track rows — same order as label rows */}
          {rows.map((row, ri) => (
            <div key={ri} className="pt-1 pb-1 pr-4" style={{ minHeight: "2.25rem" }}>
              <div className="relative h-7 bg-muted/20 rounded" style={{ minWidth: MIN_TRACK_PX }}>
                {dayColumns.map((day, i) => (
                  <div key={i} className="absolute top-0 h-full border-l border-border/30" style={{ left: pct(day.getTime() - from.getTime()) }} />
                ))}
                {row.slots.map((slot, si) => {
                  const label = slot.isOverride && slot.replacesUserName
                    ? `${slot.userInitials} → ${slot.replacesUserName.split(" ").map((p: string) => p[0]).join("")}`
                    : slot.userInitials;
                  const tooltipText = slot.isOverride
                    ? slot.replacesUserName
                      ? `${slot.userName} replacing ${slot.replacesUserName}`
                      : `${slot.userName} (extra coverage)`
                    : slot.userName;
                  return (
                    <button
                      key={si}
                      title={tooltipText}
                      onClick={() => setModal(slot)}
                      className="absolute top-0.5 h-6 rounded flex items-center justify-center text-xs font-semibold text-white truncate px-1 cursor-pointer hover:brightness-110 transition-all"
                      style={{
                        left: slotLeft(slot),
                        width: slotWidth(slot),
                        backgroundColor: slot.userColor || "#6366f1",
                        outline: slot.isOverride ? "2px solid rgba(255,255,255,0.6)" : "none",
                      }}
                    >
                      {label}
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Slot detail modal */}
      {modal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm" onClick={() => setModal(null)}>
          <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-sm mx-4" onClick={(e) => e.stopPropagation()}>
            {/* Header */}
            <div className="flex items-center justify-between px-5 py-4 border-b border-border">
              <div className="flex items-center gap-2.5">
                <div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: modal.userColor || "#6366f1" }} />
                <h2 className="text-sm font-semibold text-foreground">
                  {modal.userName}
                  {modal.isOverride && (
                    <span className="ml-1.5 text-xs font-normal text-muted-foreground">(override)</span>
                  )}
                </h2>
              </div>
              <button onClick={() => setModal(null)} className="text-muted-foreground hover:text-foreground">
                <X size={16} />
              </button>
            </div>

            {/* Body */}
            <div className="px-5 py-4 space-y-3">
              {modal.replacesUserName && (
                <div className="flex items-center gap-2 text-xs text-muted-foreground bg-muted/50 rounded-lg px-3 py-2">
                  <Pencil size={12} />
                  Replacing <span className="font-medium text-foreground">{modal.replacesUserName}</span>
                </div>
              )}
              <div className="grid grid-cols-2 gap-3 text-xs">
                <div className="flex flex-col gap-0.5">
                  <span className="text-muted-foreground font-medium uppercase tracking-wide text-[10px]">Starts</span>
                  <span className="text-foreground">{fmtDateTime(modal.startsAt)}</span>
                </div>
                <div className="flex flex-col gap-0.5">
                  <span className="text-muted-foreground font-medium uppercase tracking-wide text-[10px]">Ends</span>
                  <span className="text-foreground">{fmtDateTime(modal.endsAt)}</span>
                </div>
              </div>
              <div className="flex flex-col gap-0.5 text-xs">
                <span className="text-muted-foreground font-medium uppercase tracking-wide text-[10px]">Layer</span>
                <span className="text-foreground">{modal.layerName}</span>
              </div>
            </div>

            {/* Footer */}
            <div className="flex items-center justify-between px-5 py-4 border-t border-border">
              <div className="flex items-center gap-2">
                {modal.isOverride && onDeleteSlot && (
                  <button
                    onClick={() => { onDeleteSlot(modal); setModal(null); }}
                    className="flex items-center gap-1.5 text-xs text-destructive hover:bg-destructive/10 rounded-lg px-3 py-1.5 transition-colors"
                  >
                    <Trash2 size={13} /> Delete override
                  </button>
                )}
              </div>
              <button
                onClick={() => setModal(null)}
                className="rounded-lg border border-border px-4 py-1.5 text-xs font-medium hover:bg-muted transition-colors"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
