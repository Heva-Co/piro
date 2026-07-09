import { cn } from "@/lib/utils";

const STATUS_STYLES: Record<string, { classes: string; label: string }> = {
  UP: { classes: "bg-green-500 text-white", label: "Up" },
  DOWN: { classes: "bg-destructive text-white", label: "Down" },
  DEGRADED: { classes: "bg-amber-500 text-white", label: "Degraded" },
  MAINTENANCE: { classes: "bg-blue-500 text-white", label: "Maintenance" },
  NO_DATA: { classes: "bg-muted text-black", label: "No Data" },
  FAILURE: { classes: "bg-orange-500 text-white", label: "Check Error" },
  MONITOR_OUTAGE: { classes: "bg-yellow-100 text-yellow-800 border border-yellow-300", label: "Monitor Outage" },
};

interface StatusPillProps {
  status: string;
  dataType?: string | null;
  className?: string;
}

export function StatusPill({ status, dataType, className }: StatusPillProps) {
  const key = dataType === "MONITOR_OUTAGE" ? "MONITOR_OUTAGE" : (status ?? "").toUpperCase();
  const { classes, label } = STATUS_STYLES[key] ?? STATUS_STYLES["NO_DATA"];

  return (
    <span className={cn("inline-flex items-center rounded-lg px-3 py-1.5 border text-sm font-semibold", classes, className)}>
      {label}
    </span>
  );
}

export default StatusPill;

/** @deprecated Use StatusPill instead */
export const StatusBadge = StatusPill;
