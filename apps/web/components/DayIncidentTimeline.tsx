import type { Incident, IncidentImpactChange } from "@/lib/api";

const impactColor: Record<string, string> = {
  UP: "bg-green-500",
  DEGRADED: "bg-yellow-500",
  DOWN: "bg-red-500",
  MAINTENANCE: "bg-blue-500",
};

const impactLabel: Record<string, string> = {
  UP: "Operational",
  DEGRADED: "Degraded",
  DOWN: "Outage",
  MAINTENANCE: "Maintenance",
};

const impactTextColor: Record<string, string> = {
  UP: "text-green-700 dark:text-green-400",
  DEGRADED: "text-yellow-700 dark:text-yellow-400",
  DOWN: "text-red-700 dark:text-red-400",
  MAINTENANCE: "text-blue-700 dark:text-blue-400",
};

function fmtTime(ts: number) {
  return new Date(ts * 1000).toLocaleTimeString(undefined, {
    hour: "numeric",
    minute: "2-digit",
  });
}

interface Props {
  incidents: Incident[];
  /** Unix timestamp of the start of the day (00:00:00 UTC) */
  dayStart: number;
  /** Unix timestamp of the end of the day (23:59:59 UTC) */
  dayEnd: number;
}

export function DayIncidentTimeline({ incidents, dayStart, dayEnd }: Props) {
  // Filter incidents that overlap this day
  const relevant = incidents.filter((inc) => {
    const end = inc.endDateTime ?? dayEnd;
    return inc.startDateTime <= dayEnd && end >= dayStart;
  });

  if (relevant.length === 0) {
    return (
      <div className="py-10 text-center text-sm text-muted-foreground">
        No incidents on this day — service was operational.
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {relevant.map((inc) => {
        const changes = buildTimeline(inc, dayStart, dayEnd);
        return (
          <div key={inc.id} className="space-y-2">
            <div className="flex items-start justify-between gap-2">
              <p className="font-medium text-sm">{inc.title}</p>
              <span className={`text-xs font-medium ${impactTextColor[inc.currentImpact] ?? ""}`}>
                {impactLabel[inc.currentImpact] ?? inc.currentImpact}
              </span>
            </div>

            {/* Impact timeline bar */}
            <div className="flex h-6 w-full overflow-hidden rounded gap-px">
              {changes.map((seg, idx) => (
                <div
                  key={idx}
                  className={`${impactColor[seg.impact] ?? "bg-gray-400"} h-full`}
                  style={{ flexGrow: seg.duration }}
                  title={`${impactLabel[seg.impact] ?? seg.impact} — ${fmtTime(seg.from)}–${fmtTime(seg.to)}`}
                />
              ))}
            </div>

            {/* Impact change list */}
            <ul className="space-y-1">
              {changes.map((seg, idx) => (
                <li key={idx} className="flex items-center gap-2 text-xs text-muted-foreground">
                  <span className={`inline-block w-2 h-2 rounded-full ${impactColor[seg.impact] ?? "bg-gray-400"}`} />
                  <span className="tabular-nums">{fmtTime(seg.from)}</span>
                  <span>—</span>
                  <span className={impactTextColor[seg.impact] ?? ""}>
                    {impactLabel[seg.impact] ?? seg.impact}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        );
      })}
    </div>
  );
}

interface Segment {
  impact: string;
  from: number;
  to: number;
  duration: number;
}

function buildTimeline(inc: Incident, dayStart: number, dayEnd: number): Segment[] {
  const incStart = Math.max(inc.startDateTime, dayStart);
  const incEnd = Math.min(inc.endDateTime ?? dayEnd, dayEnd);
  const totalDuration = dayEnd - dayStart;

  const changes: IncidentImpactChange[] = inc.impactChanges?.length
    ? [...inc.impactChanges].sort((a, b) => a.timestamp - b.timestamp)
    : [];

  if (changes.length === 0) {
    // No granular data — show the whole incident at currentImpact
    return [
      {
        impact: inc.currentImpact,
        from: incStart,
        to: incEnd,
        duration: (incEnd - incStart) / totalDuration,
      },
    ];
  }

  const segments: Segment[] = [];

  // Build contiguous segments from impact changes
  for (let i = 0; i < changes.length; i++) {
    const from = Math.max(changes[i].timestamp, incStart);
    const to = Math.min(
      i + 1 < changes.length ? changes[i + 1].timestamp : incEnd,
      incEnd
    );
    if (from >= to) continue;
    segments.push({
      impact: changes[i].impact,
      from,
      to,
      duration: (to - from) / totalDuration,
    });
  }

  return segments.length ? segments : [
    {
      impact: inc.currentImpact,
      from: incStart,
      to: incEnd,
      duration: (incEnd - incStart) / totalDuration,
    },
  ];
}
