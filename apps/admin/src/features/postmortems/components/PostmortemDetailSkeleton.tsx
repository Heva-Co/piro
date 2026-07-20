import { Skeleton } from "@/components/ui/skeleton";

// Stand-in for the postmortem editor while it loads: header, analysis card, sidebar cards.
function PostmortemDetailSkeleton() {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <Skeleton className="h-6 w-64" />
        <Skeleton className="h-4 w-40" />
      </div>
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2 rounded-xl border bg-card p-5">
          <Skeleton className="mb-4 h-4 w-24" />
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="mb-5 flex flex-col gap-2">
              <Skeleton className="h-4 w-32" />
              <Skeleton className="h-16 w-full" />
            </div>
          ))}
        </div>
        <div className="flex flex-col gap-6">
          <div className="rounded-xl border bg-card p-5">
            <Skeleton className="mb-3 h-4 w-32" />
            <Skeleton className="h-9 w-full" />
          </div>
          <div className="rounded-xl border bg-card p-5">
            <Skeleton className="mb-3 h-4 w-24" />
            <Skeleton className="h-24 w-full" />
          </div>
        </div>
      </div>
    </div>
  );
}

export default PostmortemDetailSkeleton;
