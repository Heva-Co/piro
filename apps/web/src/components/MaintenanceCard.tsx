import type { Maintenance } from "@/src/lib/api";

interface Props {
  maintenance: Maintenance;
}

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
  if (seconds < 60) return `${seconds} seconds`;
  const mins = Math.floor(seconds / 60);
  if (mins < 60) return `${mins} minute${mins !== 1 ? "s" : ""}`;
  const hrs = Math.floor(mins / 60);
  const remMins = mins % 60;
  if (remMins === 0) return `${hrs} hour${hrs !== 1 ? "s" : ""}`;
  return `${hrs}h ${remMins}m`;
}

const STATUS_LABEL: Record<string, string> = {
  Scheduled: "SCHEDULED",
  Ongoing: "ONGOING",
  Completed: "COMPLETED",
  Cancelled: "CANCELLED",
};

const STATUS_COLOR: Record<string, string> = {
  Scheduled: "text-blue-500",
  Ongoing: "text-blue-600",
  Completed: "text-muted-foreground",
  Cancelled: "text-muted-foreground",
};

export function MaintenanceCard({ maintenance }: Props) {
  const nextEvent = maintenance.upcomingEvents[0];
  const eventStatus = nextEvent?.status ?? "Scheduled";
  const statusColor = STATUS_COLOR[eventStatus];
  const statusLabel = STATUS_LABEL[eventStatus];

  return (
    <div className="rounded-3xl border p-5 flex flex-col gap-3">
      <div className="flex flex-col gap-0.5">
        <span className={`text-xs font-semibold uppercase tracking-wide ${statusColor}`}>
          {statusLabel}
        </span>
        <h3 className="font-semibold text-base">{maintenance.title}</h3>
        {maintenance.description && (
          <p className="text-sm text-muted-foreground">{maintenance.description}</p>
        )}
      </div>

      {nextEvent && (
        <div className="flex items-center justify-between gap-2 text-xs font-medium mt-1">
          <span className="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">
            {fmtTs(nextEvent.startDateTime)}
          </span>
          <span className="relative flex-1 text-center">
            <span className="absolute inset-y-1/2 left-0 right-0 border-t" />
            <span className="relative z-10 bg-background px-2 text-muted-foreground">
              {fmtDuration(maintenance.durationSeconds)}
            </span>
          </span>
          <span className="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">
            {fmtTs(nextEvent.endDateTime)}
          </span>
        </div>
      )}
    </div>
  );
}
