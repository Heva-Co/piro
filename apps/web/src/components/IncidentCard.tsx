import { ChevronDown } from "lucide-react";
import type { Incident } from "@/src/lib/actions/incidents";
import Link from "next/link";

const statusColor: Record<string, string> = {
  Investigating: "text-amber-500",
  Identified: "text-orange-500",
  Monitoring: "text-blue-500",
  Resolved: "text-green-500",
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

function fmtDuration(from: number, to: number): string {
  const secs = to - from;
  if (secs < 60) return `${secs} seconds`;
  const mins = Math.floor(secs / 60);
  if (mins < 60) return `${mins} minute${mins !== 1 ? "s" : ""}`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs} hour${hrs !== 1 ? "s" : ""}`;
  const days = Math.floor(hrs / 24);
  return `${days} day${days !== 1 ? "s" : ""}`;
}

interface Props {
  incident: Incident;
}

export function IncidentCard({ incident }: Props) {
  const endForDuration = incident.endDateTime ?? Math.floor(Date.now() / 1000);
  const lastUpdated = incident.endDateTime ?? incident.startDateTime;

  return (
    <div className="rounded-3xl border p-4 flex flex-col gap-2">
      <div className="flex flex-col gap-0.5">
        <span
          className={`text-xs font-semibold uppercase tracking-wide ${statusColor[incident.status] ?? "text-muted-foreground"}`}
        >
          {incident.status}
        </span>
        <Link href={`/incidents/${incident.id}`} className="font-semibold text-base hover:underline">
          {incident.title}
        </Link>
      </div>

      <div className="flex items-center justify-between gap-2 text-xs font-medium mt-1">
        <span className="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">
          {fmtTs(incident.startDateTime)}
        </span>
        <span className="relative flex-1 text-center">
          <span className="absolute inset-y-1/2 left-0 right-0 border-t" />
          <span className="relative z-10 bg-background px-2 text-muted-foreground">
            {fmtDuration(incident.startDateTime, endForDuration)}
          </span>
        </span>
        {incident.endDateTime ? (
          <span className="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">
            {fmtTs(incident.endDateTime)}
          </span>
        ) : (
          <span className="shrink-0 rounded-full border px-3 py-1.5">Ongoing</span>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-2 text-xs font-medium mt-1">
        <div className="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground">
          <span>Last Updated</span>
          <span>{fmtTs(lastUpdated)}</span>
        </div>
        <div className="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground">
          <span>Status</span>
          <span className={statusColor[incident.status] ?? ""}>{incident.status}</span>
        </div>
        <Link
          href={`/incidents/${incident.id}`}
          className="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground hover:bg-muted transition-colors"
        >
          <span>View updates</span>
          <ChevronDown className="size-3.5 -rotate-90" />
        </Link>
      </div>
    </div>
  );
}
