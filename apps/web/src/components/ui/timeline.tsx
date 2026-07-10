import type { ReactNode } from "react";
import { cn } from "@/src/lib/utils";

const statusColor: Record<string, string> = {
  Investigating: "bg-red-500",
  Identified:    "bg-orange-500",
  Monitoring:    "bg-amber-500",
  Resolved:      "bg-green-500",
};

export interface TimelineItemData {
  id: number | string;
  status: string;
  timestamp: number;
  body: ReactNode;
  isFirst?: boolean;
}

interface TimelineProps {
  items: TimelineItemData[];
  className?: string;
}

function fmtTs(ts: number): string {
  return new Date(ts * 1000).toLocaleString(undefined, {
    month: "short", day: "numeric", year: "numeric",
    hour: "numeric", minute: "2-digit",
  });
}

function timeAgo(ts: number): string {
  const diff = Math.floor((Date.now() - ts * 1000) / 1000);
  if (diff < 60) return "just now";
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
  return `${Math.floor(diff / 86400)}d ago`;
}

export function Timeline({ items, className }: TimelineProps) {
  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground">No updates yet.</p>;
  }

  return (
    <ol className={cn("relative flex flex-col gap-0", className)}>
      {items.map((item, i) => {
        const isLast = i === items.length - 1;
        const dot = statusColor[item.status] ?? "bg-muted-foreground";
        const isFirst = i === 0;
        return (
          <li key={item.id} className="flex gap-4">
            {/* Icon + connector */}
            <div className="flex flex-col items-center">
              <div className={cn(
                "size-3 rounded-full shrink-0 mt-1.5 ring-4 ring-background",
                isFirst ? dot : "bg-border"
              )} />
              {!isLast && <div className="w-px flex-1 bg-border mt-1 mb-1" />}
            </div>

            {/* Content */}
            <div className={cn("flex flex-col gap-1 pb-7", isLast && "pb-0")}>
              <div className="flex items-baseline gap-2 flex-wrap">
                <span className="font-semibold text-sm">{item.status}</span>
                <span className="text-xs text-muted-foreground">{timeAgo(item.timestamp)}</span>
              </div>
              <p className="text-xs text-muted-foreground">{fmtTs(item.timestamp)}</p>
              <p className="text-sm mt-0.5 leading-relaxed">{item.body}</p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
