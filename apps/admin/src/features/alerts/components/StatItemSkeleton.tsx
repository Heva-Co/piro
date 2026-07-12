import { Skeleton } from "@/components/ui/skeleton";

function StatItemSkeleton() {
  return (
    <div className="flex items-center gap-3 px-5 py-3">
      <Skeleton className="size-5 rounded-full" />
      <div className="flex flex-col gap-1">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="h-5 w-8" />
      </div>
    </div>
  );
}

export default StatItemSkeleton