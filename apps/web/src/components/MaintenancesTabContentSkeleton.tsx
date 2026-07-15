import { Skeleton } from "@/src/components/ui/skeleton";

/** Matches the Maintenances tab's content layout (a short list of maintenance cards) — shown while its data loads. */
export function MaintenancesTabContentSkeleton() {
  return (
    <div className="p-5 flex flex-col gap-3">
      <Skeleton className="h-20 w-full rounded-xl" />
      <Skeleton className="h-20 w-full rounded-xl" />
    </div>
  );
}
