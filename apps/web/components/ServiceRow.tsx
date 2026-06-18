import Link from "next/link";
import { CircleCheck, CircleX, TriangleAlert, Wrench, CircleMinus } from "lucide-react";
import type { PublicService, ServiceOverviewDto } from "@/lib/api";
import { formatLatency } from "@/lib/utils";
import { StatusBarCalendar } from "./StatusBarCalendar";

const statusIcon = {
  UP: CircleCheck,
  DEGRADED: TriangleAlert,
  DOWN: CircleX,
  MAINTENANCE: Wrench,
  NO_DATA: CircleMinus,
} as const;

const statusColor: Record<string, string> = {
  UP: "text-green-500",
  DEGRADED: "text-amber-500",
  DOWN: "text-red-500",
  MAINTENANCE: "text-indigo-500",
  NO_DATA: "text-muted-foreground",
};

interface Props {
  service: PublicService;
  overview: ServiceOverviewDto | null;
}

export function ServiceRow({ service, overview }: Props) {
  const Icon = statusIcon[service.status] ?? statusIcon.NO_DATA;

  const fromDate = overview
    ? new Date(overview.fromTimestamp * 1000).toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
      })
    : null;

  const toDate = overview
    ? new Date(overview.toTimestamp * 1000).toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
      })
    : null;

  return (
    <Link
      href={`/services/${service.slug}`}
      className="flex flex-col gap-3 px-5 py-4 hover:bg-muted/20 transition-colors"
    >
      {/* Top row */}
      <div className="flex items-center gap-3">
        {service.imageUrl && (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={service.imageUrl}
            alt={service.name}
            className="size-8 rounded-lg object-cover shrink-0 hidden sm:block"
          />
        )}

        <div className="flex-1 min-w-0">
          <p className="font-medium text-sm text-foreground truncate">{service.name}</p>
          {service.description && (
            <p className="text-xs text-muted-foreground truncate mt-0.5">{service.description}</p>
          )}
        </div>

        <div className="flex items-center gap-3 shrink-0">
          {overview && (
            <div className="flex flex-col items-end gap-0.5">
              <span className="text-sm font-semibold text-foreground">
                {overview.uptimePercent.toFixed(1)}%
              </span>
              {overview.overallAvgLatencyMs != null && (
                <span className="text-xs text-muted-foreground">
                  {formatLatency(overview.overallAvgLatencyMs)}
                </span>
              )}
            </div>
          )}
          <Icon className={`size-5 ${statusColor[service.status]}`} />
        </div>
      </div>

      {/* Uptime bar */}
      {overview && overview.dailyData.length > 0 && (
        <div className="flex flex-col gap-1.5">
          <StatusBarCalendar data={overview.dailyData} barHeight={36} radius={6} />
          <div className="flex justify-between text-xs text-muted-foreground">
            <span>{fromDate}</span>
            <span>{toDate}</span>
          </div>
        </div>
      )}
    </Link>
  );
}
