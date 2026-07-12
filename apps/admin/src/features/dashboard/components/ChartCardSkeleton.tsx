import { Skeleton } from "@/components/ui/skeleton";

interface Props {
  title: string;
}

function ChartCardSkeleton(props: Props) {
  const { title } = props;

  return (
    <div className="flex-1 bg-card rounded-lg border border-border shadow-sm p-5">
      <h3 className="text-sm font-medium text-foreground mb-3">{title}</h3>
      <Skeleton className="h-55 w-full" />
    </div>
  );
}

export default ChartCardSkeleton;
