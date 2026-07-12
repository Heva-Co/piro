"use client";

import { useState } from "react";
import type { Incident, Maintenance } from "@/src/lib/api";
import { IncidentCard } from "./IncidentCard";

type Tab = "incidents" | "maintenances";

function EmptyState({ message }: { message: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 gap-4 text-center">
      <img src="/no-data.svg" alt="" className="w-48 h-48 opacity-90" />
      <p className="text-sm text-muted-foreground max-w-xs">{message}</p>
    </div>
  );
}

const maintenanceEventColor: Record<string, string> = {
  Scheduled: "text-blue-500",
  Ongoing: "text-blue-600",
  Completed: "text-green-500",
  Cancelled: "text-muted-foreground",
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

function fmtDuration(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const mins = Math.floor(seconds / 60);
  if (mins < 60) return `${mins}m`;
  const hrs = Math.floor(mins / 60);
  const remMins = mins % 60;
  return remMins === 0 ? `${hrs}h` : `${hrs}h ${remMins}m`;
}

function groupByMonth<T>(items: T[], getTs: (item: T) => number): { label: string; items: T[] }[] {
  const map = new Map<string, T[]>();
  for (const item of items) {
    const label = new Date(getTs(item) * 1000).toLocaleString(undefined, {
      month: "long",
      year: "numeric",
    });
    if (!map.has(label)) map.set(label, []);
    map.get(label)!.push(item);
  }
  return Array.from(map.entries()).map(([label, items]) => ({ label, items }));
}

interface Props {
  incidents: Incident[];
  maintenances: Maintenance[];
}

export function IncidentHistoryClient({ incidents, maintenances }: Props) {
  const [tab, setTab] = useState<Tab>("incidents");

  const sortedIncidents = [...incidents].sort((a, b) => b.startDateTime - a.startDateTime);
  const sortedMaintenances = [...maintenances].sort((a, b) => b.startDateTime - a.startDateTime);

  const incidentGroups = groupByMonth(sortedIncidents, (i) => i.startDateTime);
  const maintenanceGroups = groupByMonth(sortedMaintenances, (m) => m.startDateTime);

  const tabs: { key: Tab; label: string; count: number }[] = [
    { key: "incidents", label: "Incidents", count: incidents.length },
    { key: "maintenances", label: "Maintenances", count: maintenances.length },
  ];

  return (
    <div className="flex flex-col gap-6">
      <div className="flex gap-1 border-b">
        {tabs.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
              tab === t.key
                ? "border-foreground text-foreground"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            {t.label}
            <span className="ml-1.5 text-xs text-muted-foreground">({t.count})</span>
          </button>
        ))}
      </div>

      {tab === "incidents" && (
        <div className="flex flex-col gap-8">
          {incidentGroups.length === 0 ? (
            <EmptyState message="No incidents have been recorded yet." />
          ) : (
            incidentGroups.map((group) => (
              <section key={group.label} className="flex flex-col gap-3">
                <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">
                  {group.label}
                </h2>
                {group.items.map((incident) => (
                  <IncidentCard key={incident.id} incident={incident} />
                ))}
              </section>
            ))
          )}
        </div>
      )}

      {tab === "maintenances" && (
        <div className="flex flex-col gap-8">
          {maintenanceGroups.length === 0 ? (
            <EmptyState message="No maintenances have been scheduled yet." />
          ) : (
            maintenanceGroups.map((group) => (
              <section key={group.label} className="flex flex-col gap-3">
                <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">
                  {group.label}
                </h2>
                {group.items.map((maintenance) => {
                  const latestEvent = [...maintenance.upcomingEvents].sort(
                    (a, b) => b.startDateTime - a.startDateTime
                  )[0];
                  const statusLabel = latestEvent?.status ?? "Scheduled";
                  const colorClass = maintenanceEventColor[statusLabel] ?? "text-muted-foreground";

                  return (
                    <div key={maintenance.id} className="rounded-3xl border p-4 flex flex-col gap-2">
                      <div className="flex flex-col gap-0.5">
                        <span className={`text-xs font-semibold uppercase tracking-wide ${colorClass}`}>
                          {statusLabel}
                        </span>
                        <span className="font-semibold text-base">{maintenance.title}</span>
                        {maintenance.description && (
                          <p className="text-sm text-muted-foreground">{maintenance.description}</p>
                        )}
                      </div>
                      {latestEvent && (
                        <div className="flex items-center justify-between gap-2 text-xs font-medium mt-1">
                          <span className="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">
                            {fmtTs(latestEvent.startDateTime)}
                          </span>
                          <span className="relative flex-1 text-center">
                            <span className="absolute inset-y-1/2 left-0 right-0 border-t" />
                            <span className="relative z-10 bg-background px-2 text-muted-foreground">
                              {fmtDuration(maintenance.durationSeconds)}
                            </span>
                          </span>
                          <span className="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">
                            {fmtTs(latestEvent.endDateTime)}
                          </span>
                        </div>
                      )}
                      {maintenance.serviceSlugs.length > 0 && (
                        <p className="text-xs text-muted-foreground">
                          {maintenance.isGlobal
                            ? "All services affected"
                            : `${maintenance.serviceSlugs.length} service${maintenance.serviceSlugs.length !== 1 ? "s" : ""} affected`}
                        </p>
                      )}
                    </div>
                  );
                })}
              </section>
            ))
          )}
        </div>
      )}
    </div>
  );
}
