"use client";

import { useState } from "react";
import type { PublicService, ServiceOverviewDto, DailyStatsDto, StatusPoint } from "@/lib/api";
import { formatLatency } from "@/lib/utils";
import { StatusBarCalendar } from "./StatusBarCalendar";
import { LatencyTrendChart } from "./LatencyTrendChart";
import { IncidentCard } from "./IncidentCard";
import { MaintenanceCard } from "./MaintenanceCard";
import { PerMinuteStatusGrid } from "./PerMinuteStatusGrid";
import type { Incident, Maintenance } from "@/lib/api";

type Tab = "status" | "latency" | "incidents" | "maintenances";
type LatencyMetric = "avg" | "min" | "max";

const DAY_OPTIONS = [
  { label: "7 Days", value: 7 },
  { label: "14 Days", value: 14 },
  { label: "30 Days", value: 30 },
  { label: "60 Days", value: 60 },
  { label: "90 Days", value: 90 },
];

const statusLabel: Record<string, string> = {
  UP: "Service Operational",
  DEGRADED: "Partial Outage",
  DOWN: "Major Outage",
  MAINTENANCE: "Under Maintenance",
  NO_DATA: "No Status Data",
  FAILURE: "No Status Data",
};

const statusClass: Record<string, string> = {
  UP: "text-green-600 dark:text-green-400",
  DEGRADED: "text-yellow-600 dark:text-yellow-400",
  DOWN: "text-red-600 dark:text-red-400",
  MAINTENANCE: "text-blue-600 dark:text-blue-400",
  NO_DATA: "text-muted-foreground",
  FAILURE: "text-muted-foreground",
};

function fmtTs(ts: number): string {
  return new Date(ts * 1000).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

interface Props {
  service: PublicService;
  initialOverview: ServiceOverviewDto;
  incidents: Incident[];
  maintenances: Maintenance[];
}

export function ServiceDetailClient({
  service,
  initialOverview,
  incidents,
  maintenances,
}: Props) {
  const [tab, setTab] = useState<Tab>("status");
  const [overview, setOverview] = useState(initialOverview);
  const [selectedDays, setSelectedDays] = useState(service.historyDaysDesktop);
  const [loading, setLoading] = useState(false);
  const [latencyMetric, setLatencyMetric] = useState<LatencyMetric>("avg");

  // Day detail dialog
  const [dayDetailOpen, setDayDetailOpen] = useState(false);
  const [dayDetailDay, setDayDetailDay] = useState<DailyStatsDto | null>(null);
  const [dayDetailHistory, setDayDetailHistory] = useState<StatusPoint[]>([]);
  const [dayDetailLoading, setDayDetailLoading] = useState(false);

  const availableDayOptions = DAY_OPTIONS.filter((o) => o.value <= service.historyDaysDesktop);

  async function changeDays(days: number) {
    setSelectedDays(days);
    setLoading(true);
    try {
      const res = await fetch(`/api/v1/public/services/${service.slug}/overview?days=${days}`);
      if (res.ok) setOverview(await res.json());
    } catch (e) {
      console.error("overview fetch failed", e);
    } finally {
      setLoading(false);
    }
  }

  async function openDayDetail(day: DailyStatsDto) {
    setDayDetailDay(day);
    setDayDetailOpen(true);
    setDayDetailHistory([]);
    setDayDetailLoading(true);
    try {
      const from = day.timestamp;
      const to = day.timestamp + 86399;
      const res = await fetch(`/api/v1/public/services/${service.slug}/history?from=${from}&to=${to}`);
      if (res.ok) setDayDetailHistory(await res.json());
    } catch (e) {
      console.error("day detail fetch failed", e);
    } finally {
      setDayDetailLoading(false);
    }
  }

  const fromDate = new Date(overview.fromTimestamp * 1000).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  });

  const toDate = new Date(overview.toTimestamp * 1000).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  });

  const tabs: { id: Tab; label: string; badge?: number }[] = [
    { id: "status", label: "Status" },
    { id: "latency", label: "Latency" },
    { id: "incidents", label: "Incidents", badge: incidents.length > 0 ? incidents.length : undefined },
    { id: "maintenances", label: "Maintenances" },
  ];

  return (
    <>
      {/* Status card */}
      <div className="bg-background rounded-3xl border p-5 flex flex-col gap-3">
        <div className="flex flex-col gap-0.5">
          <p className="text-sm font-medium">Last Updated</p>
          <p className="text-xs text-muted-foreground">{fmtTs(overview.lastUpdatedAt)}</p>
        </div>
        <div className="flex items-end justify-between">
          <div className="flex flex-col gap-1">
            <p className={`text-2xl font-semibold ${statusClass[overview.currentStatus]}`}>
              {statusLabel[overview.currentStatus]}
            </p>
            <p className="text-xs text-muted-foreground">Latest Status</p>
          </div>
          {overview.lastLatencyMs && (
            <div className="flex flex-col items-end gap-1">
              <p className="text-2xl font-semibold">{formatLatency(overview.lastLatencyMs)}</p>
              <p className="text-xs text-muted-foreground">Latest Latency</p>
            </div>
          )}
        </div>
      </div>

      {/* Detail tabs */}
      <div className={`bg-background rounded-3xl border transition-opacity ${loading ? "opacity-50" : ""}`}>
        {/* Tab header */}
        <div className="flex items-center justify-between px-5 pt-4 pb-0 gap-4 border-b">
          <div className="flex bg-transparent p-0 h-auto rounded-none gap-0">
            {tabs.map(({ id, label, badge }) => (
              <button
                key={id}
                onClick={() => setTab(id)}
                className={`relative rounded-none bg-transparent px-4 pb-3 pt-1 text-sm font-medium shadow-none transition-colors ${
                  tab === id
                    ? "text-foreground after:scale-x-100"
                    : "text-muted-foreground hover:text-foreground after:scale-x-0"
                } after:absolute after:bottom-0 after:left-0 after:right-0 after:h-0.5 after:rounded-full after:bg-foreground after:transition-transform`}
              >
                {label}
                {badge !== undefined && (
                  <span className="ml-1.5 inline-flex size-4 items-center justify-center rounded-full bg-muted text-[10px] text-destructive font-semibold">
                    {badge}
                  </span>
                )}
              </button>
            ))}
          </div>

          <select
            value={selectedDays}
            onChange={(e) => changeDays(Number(e.target.value))}
            className="text-xs rounded-full border px-3 h-8 bg-background mb-2 shrink-0"
          >
            {availableDayOptions.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Status tab */}
        {tab === "status" && (
          <div className="p-5 flex flex-col gap-5">
            <div className="flex flex-col gap-0.5">
              <p className="text-3xl font-bold">{overview.uptimePercent.toFixed(1)}%</p>
              <p className="text-xs text-muted-foreground">Uptime</p>
            </div>

            {overview.dailyData.length > 0 ? (
              <div className="flex flex-col gap-1">
                <StatusBarCalendar data={overview.dailyData} onDayClick={openDayDetail} />
                <div className="flex justify-between text-xs text-muted-foreground mt-1">
                  <span>{fromDate}</span>
                  <span>{toDate}</span>
                </div>
              </div>
            ) : (
              <div className="h-12 rounded-lg bg-muted/50 flex items-center justify-center text-xs text-muted-foreground">
                No history data yet
              </div>
            )}
          </div>
        )}

        {/* Latency tab */}
        {tab === "latency" && (
          <div className="p-5 flex flex-col gap-5">
            {overview.overallAvgLatencyMs !== null ? (
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
                    <p className="text-xl font-bold">{formatLatency(overview.overallMinLatencyMs)}</p>
                    <p className="text-xs text-muted-foreground">Min Latency</p>
                  </div>
                  <div className="flex flex-col items-center gap-0.5">
                    <p className="text-xl font-bold">{formatLatency(overview.overallAvgLatencyMs)}</p>
                    <p className="text-xs text-muted-foreground">Average Latency</p>
                  </div>
                  <div className="flex flex-col items-end gap-0.5">
                    <p className="text-xl font-bold">{formatLatency(overview.overallMaxLatencyMs)}</p>
                    <p className="text-xs text-muted-foreground">Max Latency</p>
                  </div>
                </div>

                <LatencyTrendChart data={overview.dailyData} metric={latencyMetric} />
              </>
            ) : (
              <p className="text-sm text-muted-foreground py-8 text-center">
                No latency data available
              </p>
            )}
          </div>
        )}

        {/* Incidents tab */}
        {tab === "incidents" && (
          <div className="p-5 flex flex-col gap-3">
            {incidents.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-8">No incidents recorded</p>
            ) : (
              incidents.map((incident) => <IncidentCard key={incident.id} incident={incident} />)
            )}
          </div>
        )}

        {/* Maintenances tab */}
        {tab === "maintenances" && (
          <div className="p-5 flex flex-col gap-3">
            {maintenances.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-8">
                No maintenances scheduled
              </p>
            ) : (
              maintenances.map((m) => <MaintenanceCard key={m.id} maintenance={m} />)
            )}
          </div>
        )}
      </div>

      {/* Day detail dialog */}
      {dayDetailOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50" onClick={() => setDayDetailOpen(false)} />
          <div className="relative bg-background rounded-2xl border shadow-lg max-w-2xl w-full max-h-[90vh] overflow-y-auto p-6 flex flex-col gap-4">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-semibold">
                  {dayDetailDay
                    ? new Date(dayDetailDay.timestamp * 1000).toLocaleDateString(undefined, {
                        month: "short",
                        day: "numeric",
                        year: "numeric",
                      })
                    : ""}
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

            {dayDetailLoading ? (
              <div className="py-12 text-center text-sm text-muted-foreground">Loading…</div>
            ) : (
              <PerMinuteStatusGrid history={dayDetailHistory} />
            )}
          </div>
        </div>
      )}
    </>
  );
}
