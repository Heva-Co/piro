import type { ServiceOverviewDto } from "@/src/lib/actions/services";
import { formatLatency, formatLocalDateTime } from "@/src/lib/utils";

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

interface Props {
  overview: ServiceOverviewDto;
}

export function ServiceStatusCard({ overview }: Props) {
  // The backend (ServiceStatusService) already derives this from incidents + maintenance
  // windows — never recompute it client-side, or the two can silently disagree.
  const currentStatus = overview.currentStatus;

  return (
    <div className="bg-background rounded-3xl border p-5 flex flex-col gap-3">
      <div className="flex flex-col gap-0.5">
        <p className="text-sm font-medium">Last Updated</p>
        <p className="text-xs text-muted-foreground">{formatLocalDateTime(overview.lastUpdatedAt)}</p>
      </div>
      <div className="flex flex-wrap items-end justify-between gap-x-4 gap-y-2">
        <div className="flex flex-col gap-1">
          <p className={`text-2xl font-semibold ${statusClass[currentStatus] ?? statusClass.NO_DATA}`}>
            {statusLabel[currentStatus] ?? currentStatus}
          </p>
          <p className="text-xs text-muted-foreground">Current Status</p>
        </div>
        {overview.lastLatencyMs && (
          <div className="flex flex-col items-start sm:items-end gap-1">
            <p className="text-2xl font-semibold">{formatLatency(overview.lastLatencyMs)}</p>
            <p className="text-xs text-muted-foreground">Latest Latency</p>
          </div>
        )}
      </div>
    </div>
  );
}
