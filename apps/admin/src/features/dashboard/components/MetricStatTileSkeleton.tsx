import { Skeleton } from "@/components/ui/skeleton";

interface Props {
  label: string;
}

function MetricStatTileSkeleton(props: Props) {
  const { label } = props;

  return (
    <div className="bg-card rounded-lg border border-border p-5 shadow-sm">
      <p className="text-sm text-muted-foreground mb-1">{label}</p>
      <Skeleton className="h-9 w-16 mb-1" />
      <Skeleton className="h-3 w-24" />
    </div>
  );
}

export default MetricStatTileSkeleton;
