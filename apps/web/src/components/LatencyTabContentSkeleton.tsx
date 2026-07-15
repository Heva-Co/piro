import { Skeleton } from "@/src/components/ui/skeleton";

/** Matches the Latency tab's content layout (metric toggle + trend chart) — shown while its data loads. */
export function LatencyTabContentSkeleton() {
  return (
    <div className="p-5 flex flex-col gap-5">
      <div className="flex items-center gap-2">
        <Skeleton className="h-3 w-20" />
        <div className="flex gap-1">
          <Skeleton className="h-6 w-14 rounded-full" />
          <Skeleton className="h-6 w-14 rounded-full" />
          <Skeleton className="h-6 w-14 rounded-full" />
        </div>
      </div>
      <Skeleton className="h-40 w-full rounded-lg" />
    </div>
  );
}
