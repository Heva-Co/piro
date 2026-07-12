import { useMemo, useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { useMyOnCallSlots } from "@/hooks/useOnCallMe";
import { GanttTimeline } from "@/features/oncall/components/GanttTimeline";
import { ScheduleLabel } from "@/features/profile/components/ScheduleLabel";
import type { OnCallSlot } from "@/lib/api";

function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

function startOfDay(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
}

function isoStr(d: Date): string {
  return d.toISOString();
}

// Range days are UTC-aligned (see startOfDay) — format in UTC too, so the label always
// matches the day the Gantt bars actually cover, regardless of the viewer's own timezone.
function fmtRange(from: Date, to: Date): string {
  const opts: Intl.DateTimeFormatOptions = { month: "short", day: "numeric", timeZone: "UTC" };
  return `${from.toLocaleDateString(undefined, opts)} – ${addDays(to, -1).toLocaleDateString(undefined, opts)}`;
}

interface ScheduleGroup {
  name: string;
  slots: OnCallSlot[];
  isPrimary: boolean;
}

function groupBySchedule(slots: OnCallSlot[]): ScheduleGroup[] {
  const bySchedule = new Map<string, ScheduleGroup>();
  for (const slot of slots) {
    const key = String(slot.scheduleId ?? slot.scheduleName ?? "unknown");
    if (!bySchedule.has(key)) {
      bySchedule.set(key, { name: slot.scheduleName ?? "Schedule", slots: [], isPrimary: slot.isPrimarySchedule });
    }
    bySchedule.get(key)!.slots.push(slot);
  }
  return Array.from(bySchedule.values());
}

export function MyOnCallCalendarCard() {
  const [anchor, setAnchor] = useState<Date>(startOfDay(new Date()));
  const { from, to } = useMemo(() => ({ from: anchor, to: addDays(anchor, 14) }), [anchor]);

  const { data: slots = [], isLoading } = useMyOnCallSlots(isoStr(from), isoStr(to));

  const groups = useMemo(() => groupBySchedule(slots), [slots]);
  const rows = useMemo(
    () => groups.map((group) => ({ label: <ScheduleLabel name={group.name} isPrimary={group.isPrimary} />, slots: group.slots })),
    [groups]
  );

  return (
    <div className="rounded-xl border border-border bg-card shadow-sm">
      <div className="flex items-center justify-between px-6 py-4 border-b border-border">
        <div>
          <h2 className="text-sm font-semibold">My on-call schedule</h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            Your upcoming on-call shifts across every schedule you're part of.
          </p>
        </div>
        <div className="flex items-center gap-1">
          <button
            onClick={() => setAnchor((a) => addDays(a, -14))}
            className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
            title="Previous 2 weeks"
          >
            <ChevronLeft size={16} />
          </button>
          <span className="text-xs font-medium text-muted-foreground w-32 text-center">{fmtRange(from, to)}</span>
          <button
            onClick={() => setAnchor((a) => addDays(a, 14))}
            className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
            title="Next 2 weeks"
          >
            <ChevronRight size={16} />
          </button>
        </div>
      </div>
      <div className="px-6 py-5">
        {isLoading ? (
          <div className="py-4 text-sm text-muted-foreground">Loading…</div>
        ) : rows.length === 0 ? (
          <div className="py-4 text-sm text-muted-foreground italic">
            You're not part of any on-call rotation in this range.
          </div>
        ) : (
          <GanttTimeline rows={rows} from={from} to={to} totalMs={to.getTime() - from.getTime()} />
        )}
      </div>
    </div>
  );
}
