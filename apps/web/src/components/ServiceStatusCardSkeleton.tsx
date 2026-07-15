import { Skeleton } from "@/src/components/ui/skeleton";

/** Matches ServiceStatusCard's layout — shown while its `overview` fetch is in flight. */
export function ServiceStatusCardSkeleton() {
  return (
    <div className="bg-background rounded-3xl border p-5 flex flex-col gap-3">
      <div className="flex flex-col gap-0.5">
        <Skeleton className="h-4 w-24" />
        <Skeleton className="h-3 w-32" />
      </div>
      <div className="flex flex-wrap items-end justify-between gap-x-4 gap-y-2">
        <div className="flex flex-col gap-1">
          <Skeleton className="h-7 w-40" />
          <Skeleton className="h-3 w-24" />
        </div>
        <div className="flex flex-col items-start sm:items-end gap-1">
          <Skeleton className="h-7 w-16" />
          <Skeleton className="h-3 w-20" />
        </div>
      </div>
    </div>
  );
}
