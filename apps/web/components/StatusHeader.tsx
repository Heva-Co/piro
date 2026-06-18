import type { ServiceStatus } from "@/lib/api";
import { cn } from "@/lib/utils";

const pingColor: Record<ServiceStatus, string> = {
  UP: "bg-green-500",
  DEGRADED: "bg-amber-500",
  DOWN: "bg-red-500",
  MAINTENANCE: "bg-indigo-500",
  NO_DATA: "bg-gray-400",
};

interface Props {
  status: ServiceStatus;
  text: string;
}

export function StatusHeader({ status, text }: Props) {
  return (
    <div className="rounded-3xl border px-5 py-5 flex items-center gap-4">
      <span className="relative flex size-4 shrink-0">
        <span
          className={cn(
            "absolute inline-flex h-full w-full animate-ping rounded-full opacity-75",
            pingColor[status]
          )}
        />
        <span className={cn("relative inline-flex size-4 rounded-full", pingColor[status])} />
      </span>
      <span className="text-xl sm:text-2xl font-medium">{text}</span>
    </div>
  );
}
