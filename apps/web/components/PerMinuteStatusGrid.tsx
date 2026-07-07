"use client";

import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";

interface Props {
  slug: string;
  /** Unix timestamp of the start of the day (00:00:00 UTC) */
  dayStart: number;
}

interface StatusPoint {
  timestamp: number;
  status: string;
}

const cellColor: Record<string, string> = {
  UP: "bg-green-500",
  DEGRADED: "bg-yellow-400",
  DOWN: "bg-red-500",
  MAINTENANCE: "bg-blue-500",
};

const ROWS = [
  { label: "00:00 – 05:59", start: 0 },
  { label: "06:00 – 11:59", start: 360 },
  { label: "12:00 – 17:59", start: 720 },
  { label: "18:00 – 23:59", start: 1080 },
];

export function PerMinuteStatusGrid({ slug, dayStart }: Props) {
  const [minuteMap, setMinuteMap] = useState<Map<number, string> | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch(`/api/v1/public/services/${slug}/day-status?date=${dayStart}`)
      .then((r) => r.ok ? r.json() as Promise<StatusPoint[]> : Promise.resolve([]))
      .then((points) => {
        if (cancelled) return;
        const map = new Map<number, string>();
        for (const p of points) {
          const minuteOfDay = Math.floor((p.timestamp - dayStart) / 60);
          map.set(minuteOfDay, p.status);
        }
        setMinuteMap(map);
      })
      .catch(() => { if (!cancelled) setMinuteMap(new Map()); });
    return () => { cancelled = true; };
  }, [slug, dayStart]);

  function formatMinute(m: number): string {
    const h = Math.floor(m / 60).toString().padStart(2, "0");
    const min = (m % 60).toString().padStart(2, "0");
    return `${h}:${min}`;
  }

  if (minuteMap === null) {
    return <div className="py-12 text-center text-sm text-muted-foreground">Loading…</div>;
  }

  return (
    <div className="flex flex-col gap-3 pt-2">
      {ROWS.map((row) => (
        <div key={row.start} className="flex flex-col gap-1">
          <span className="text-xs text-muted-foreground font-medium">{row.label}</span>
          <div className="flex flex-wrap gap-0.5">
            {Array.from({ length: 360 }, (_, i) => {
              const minute = row.start + i;
              const status = minuteMap!.get(minute);
              return (
                <div
                  key={minute}
                  className={cn(
                    "size-2 rounded-sm cursor-default transition-opacity hover:opacity-70",
                    status ? (cellColor[status] ?? "bg-muted") : "bg-muted"
                  )}
                  title={`${formatMinute(minute)} — ${status ?? "Operational"}`}
                />
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}
