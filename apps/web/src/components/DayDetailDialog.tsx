"use client";

import { useState } from "react";
import type { DailyStatsDto } from "@/src/lib/actions/services";
import { formatUtcDateLong } from "@/src/lib/utils";
import { StatusBarCalendar } from "./StatusBarCalendar";
import { PerMinuteStatusGrid } from "./PerMinuteStatusGrid";

interface Props {
  slug: string;
  dailyData: DailyStatsDto[];
}

export function DayDetailCalendar({ slug, dailyData }: Props) {
  const [dayDetailOpen, setDayDetailOpen] = useState(false);
  const [dayDetailDay, setDayDetailDay] = useState<DailyStatsDto | null>(null);

  function openDayDetail(day: DailyStatsDto) {
    setDayDetailDay(day);
    setDayDetailOpen(true);
  }

  return (
    <>
      <StatusBarCalendar data={dailyData} onDayClick={openDayDetail} />

      {dayDetailOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50" onClick={() => setDayDetailOpen(false)} />
          <div className="relative bg-background rounded-2xl border shadow-lg max-w-2xl w-full max-h-[90vh] overflow-y-auto p-6 flex flex-col gap-4">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-semibold">
                  {dayDetailDay ? formatUtcDateLong(dayDetailDay.timestamp) : ""}
                </h2>
                <p className="text-sm text-muted-foreground">
                  Minute-by-minute status data for this day
                </p>
              </div>
              <button
                onClick={() => setDayDetailOpen(false)}
                className="text-muted-foreground hover:text-foreground text-xl leading-none"
              >
                ✕
              </button>
            </div>

            <PerMinuteStatusGrid slug={slug} dayStart={dayDetailDay?.timestamp ?? 0} />
          </div>
        </div>
      )}
    </>
  );
}
