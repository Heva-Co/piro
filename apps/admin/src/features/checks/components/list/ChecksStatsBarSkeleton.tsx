import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";

function ChecksStatsBarSkeleton() {
  return (
    <div className="flex items-stretch rounded-xl border bg-card">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="flex flex-1 items-stretch">
          {i > 0 && <Separator orientation="vertical" />}
          <div className="flex flex-1 items-center gap-3 px-5 py-4">
            <Skeleton className="h-5 w-5 rounded-full" />
            <div className="flex flex-col gap-1.5">
              <Skeleton className="h-3 w-16" />
              <Skeleton className="h-7 w-10" />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

export default ChecksStatsBarSkeleton;
