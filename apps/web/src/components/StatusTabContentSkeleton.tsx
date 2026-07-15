import { Skeleton } from "@/src/components/ui/skeleton";

/** Matches the Status tab's content layout (uptime % + calendar) — shown while its data loads. */
export function StatusTabContentSkeleton() {
  return (
    <div className="p-5 flex flex-col gap-5">
      <div className="flex flex-col gap-0.5">
        <Skeleton className="h-9 w-24" />
        <Skeleton className="h-3 w-16" />
      </div>
      <Skeleton className="h-12 w-full rounded-lg" />
    </div>
  );
}
