import { cn } from "@/lib/utils";
import { STATUS_LABELS } from "@/constants/api";

const STATUS_BADGE_CLASSES: Record<string, string> = {
  UP: "bg-green-100 text-green-800",
  DOWN: "bg-red-100 text-red-800",
  DEGRADED: "bg-amber-100 text-amber-800",
  MAINTENANCE: "bg-blue-100 text-blue-800",
  NO_DATA: "bg-gray-100 text-gray-500",
};

interface StatusBadgeProps {
  status: string;
  className?: string;
}

export function StatusBadge({ status, className }: StatusBadgeProps) {
  const label = STATUS_LABELS[status] ?? status;
  const classes = STATUS_BADGE_CLASSES[status] ?? "bg-gray-100 text-gray-500";
  return (
    <span
      className={cn(
        "inline-flex items-center px-2 py-0.5 rounded text-xs font-medium",
        classes,
        className
      )}
    >
      {label}
    </span>
  );
}
