import { cn } from "@/src/lib/utils";
import type { ServiceStatus } from "@/src/lib/api";

const colorMap: Record<ServiceStatus, string> = {
  UP: "bg-green-500",
  DEGRADED: "bg-amber-500",
  DOWN: "bg-red-500",
  MAINTENANCE: "bg-indigo-500",
  NO_DATA: "bg-gray-400",
  FAILURE: "bg-gray-400",
};

const sizeMap = { xs: "size-1.5", sm: "size-2", md: "size-2.5" };

interface Props {
  status: ServiceStatus;
  size?: "xs" | "sm" | "md";
  className?: string;
}

export function StatusDot({ status, size = "sm", className }: Props) {
  return (
    <span
      title={status}
      className={cn("rounded-full shrink-0 inline-block", sizeMap[size], colorMap[status], className)}
    />
  );
}
