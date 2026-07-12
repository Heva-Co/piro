import { Skeleton } from "@/components/ui/skeleton";

interface Props {
  label: string;
  value: number;
  color: string;
  isLoading?: boolean;
}

function DashboardStatCard(props: Props) {
  const { label, value, color, isLoading } = props;

  return (
    <div className="bg-card rounded-lg border border-border p-5 shadow-sm">
      <p className="text-sm text-muted-foreground mb-1">{label}</p>
      {isLoading ? (
        <Skeleton className="h-9 w-12" />
      ) : (
        <p className={`text-3xl font-bold ${color}`}>{value}</p>
      )}
    </div>
  );
}

export default DashboardStatCard;
