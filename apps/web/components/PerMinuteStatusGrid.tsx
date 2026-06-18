"use client";

import { useState } from "react";
import type { StatusPoint } from "@/lib/api";
import { cn } from "@/lib/utils";

interface Props {
  history: StatusPoint[];
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

export function PerMinuteStatusGrid({ history }: Props) {
  const [tooltip, setTooltip] = useState<{ minute: number; status: string } | null>(null);

  const minuteMap = new Map<number, string>();
  for (const p of history) {
    const d = new Date(p.timestamp * 1000);
    const minuteOfDay = d.getUTCHours() * 60 + d.getUTCMinutes();
    minuteMap.set(minuteOfDay, p.status);
  }

  function formatMinute(m: number): string {
    const h = Math.floor(m / 60)
      .toString()
      .padStart(2, "0");
    const min = (m % 60).toString().padStart(2, "0");
    return `${h}:${min}`;
  }

  return (
    <div className="flex flex-col gap-3 pt-2">
      {ROWS.map((row) => (
        <div key={row.start} className="flex flex-col gap-1">
          <span className="text-xs text-muted-foreground font-medium">{row.label}</span>
          <div className="flex flex-wrap gap-0.5">
            {Array.from({ length: 360 }, (_, i) => {
              const minute = row.start + i;
              const status = minuteMap.get(minute);
              return (
                <div
                  key={minute}
                  className={cn(
                    "size-2 rounded-sm cursor-default transition-opacity hover:opacity-70",
                    status ? (cellColor[status] ?? "bg-muted") : "bg-muted"
                  )}
                  title={`${formatMinute(minute)} — ${status ?? "No data"}`}
                  onMouseEnter={() => setTooltip({ minute, status: status ?? "No data" })}
                  onMouseLeave={() => setTooltip(null)}
                />
              );
            })}
          </div>
        </div>
      ))}
      {tooltip && (
        <p className="text-xs text-muted-foreground">
          {formatMinute(tooltip.minute)} — {tooltip.status}
        </p>
      )}
    </div>
  );
}
