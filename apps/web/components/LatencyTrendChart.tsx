"use client";

import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import type { DailyStatsDto } from "@/lib/api";
import { formatLatency } from "@/lib/utils";

interface Props {
  data: DailyStatsDto[];
  metric?: "avg" | "min" | "max";
  height?: number;
}

export function LatencyTrendChart({ data, metric = "avg", height = 128 }: Props) {
  const chartData = data
    .map((d) => ({
      date: new Date(d.timestamp * 1000).toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
      }),
      value:
        metric === "avg" ? d.avgLatencyMs : metric === "min" ? d.minLatencyMs : d.maxLatencyMs,
    }))
    .filter((d) => d.value !== null && d.value > 0) as { date: string; value: number }[];

  if (chartData.length < 2) {
    return (
      <div className="flex items-center justify-center text-muted-foreground text-sm" style={{ height }}>
        {chartData.length === 1
          ? "Not enough data for trend (need at least 2 days)"
          : "No latency data available"}
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={chartData} margin={{ top: 4, right: 4, bottom: 0, left: 0 }}>
        <defs>
          <linearGradient id="latencyGradient" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.35} />
            <stop offset="95%" stopColor="#3b82f6" stopOpacity={0} />
          </linearGradient>
        </defs>
        <XAxis
          dataKey="date"
          tick={{ fontSize: 11, fill: "currentColor", opacity: 0.5 }}
          tickLine={false}
          axisLine={false}
          interval="preserveStartEnd"
        />
        <YAxis hide domain={[0, "auto"]} />
        <Tooltip
          formatter={(v) => [formatLatency(Number(v)), "Latency"]}
          contentStyle={{ fontSize: 12 }}
        />
        <Area
          type="monotone"
          dataKey="value"
          stroke="#3b82f6"
          fill="url(#latencyGradient)"
          strokeWidth={2}
          dot={false}
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}
