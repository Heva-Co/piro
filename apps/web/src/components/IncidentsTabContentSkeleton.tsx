import { Skeleton } from "@/src/components/ui/skeleton";

/** Matches the Incidents tab's content layout (a short list of incident cards) — shown while its data loads. */
export function IncidentsTabContentSkeleton() {
  return (
    <div className="p-5 flex flex-col gap-3">
      <Skeleton className="h-20 w-full rounded-xl" />
      <Skeleton className="h-20 w-full rounded-xl" />
    </div>
  );
}
