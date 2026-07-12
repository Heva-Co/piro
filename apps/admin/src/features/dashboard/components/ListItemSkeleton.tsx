import { Skeleton } from "@/components/ui/skeleton";

function ListItemSkeleton() {
  return (
    <div className="px-5 py-3 flex flex-col gap-2">
      <Skeleton className="h-4 w-2/3" />
      <Skeleton className="h-4 w-1/3" />
    </div>
  );
}

export default ListItemSkeleton;
