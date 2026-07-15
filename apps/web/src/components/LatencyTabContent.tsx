"use client";

import { useState } from "react";
import type { DailyStatsDto } from "@/src/lib/actions/services";
import { formatLatency } from "@/src/lib/utils";
import { LatencyTrendChart } from "./LatencyTrendChart";

type LatencyMetric = "avg" | "min" | "max";

interface Props {
  dailyData: DailyStatsDto[];
  overallAvgLatencyMs: number | null;
  overallMinLatencyMs: number | null;
  overallMaxLatencyMs: number | null;
}

export function LatencyTabContent({
  dailyData,
  overallAvgLatencyMs,
  overallMinLatencyMs,
  overallMaxLatencyMs,
}: Props) {
  const [latencyMetric, setLatencyMetric] = useState<LatencyMetric>("avg");

  if (overallAvgLatencyMs === null) {
    return (
      <p className="text-sm text-muted-foreground py-8 text-center">
        No latency data available
      </p>
    );
  }

  return (
    <>
      <div className="flex items-center gap-2">
        <p className="text-xs text-muted-foreground font-medium">Latency Trend</p>
        <div className="flex gap-1">
          {(
            [
              ["avg", "Avg"],
              ["min", "Min"],
              ["max", "Max"],
            ] as const
          ).map(([val, label]) => (
            <button
              key={val}
              onClick={() => setLatencyMetric(val)}
              className={`text-xs px-2 py-0.5 rounded-full border transition-colors ${
                latencyMetric === val
                  ? "bg-foreground text-background border-foreground"
                  : "text-muted-foreground hover:text-foreground border-border"
              }`}
            >
              {label} Latency
            </button>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-3 gap-4">
        <div className="flex flex-col gap-0.5">
          <p className="text-xl font-bold">{formatLatency(overallMinLatencyMs)}</p>
          <p className="text-xs text-muted-foreground">Min Latency</p>
        </div>
        <div className="flex flex-col items-center gap-0.5">
          <p className="text-xl font-bold">{formatLatency(overallAvgLatencyMs)}</p>
          <p className="text-xs text-muted-foreground">Average Latency</p>
        </div>
        <div className="flex flex-col items-end gap-0.5">
          <p className="text-xl font-bold">{formatLatency(overallMaxLatencyMs)}</p>
          <p className="text-xs text-muted-foreground">Max Latency</p>
        </div>
      </div>

      <LatencyTrendChart data={dailyData} metric={latencyMetric} />
    </>
  );
}
