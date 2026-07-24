import { Wrench } from "lucide-react";
import ListItemSkeleton from "@/features/dashboard/components/ListItemSkeleton";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
import type { MaintenanceListItem } from "@/lib/api";

interface Props {
  maintenances: MaintenanceListItem[];
  isLoading: boolean;
}

function ActiveMaintenancesCard(props: Props) {
  const { maintenances, isLoading } = props;

  return (
    <div className="bg-card rounded-lg border border-border shadow-sm overflow-hidden">
      <div className="px-5 py-4 border-b border-border">
        <h2 className="font-semibold text-foreground">Active Maintenances</h2>
      </div>
      {isLoading ? (
        <div className="divide-y divide-border">
          <ListItemSkeleton />
          <ListItemSkeleton />
        </div>
      ) : maintenances.length === 0 ? (
        <Empty className="py-10">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <Wrench />
            </EmptyMedia>
            <EmptyTitle>No active maintenances</EmptyTitle>
            <EmptyDescription>Scheduled maintenance windows will appear here.</EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : (
        <ul className="divide-y divide-border">
          {maintenances.map((m) => (
            <li key={m.id} className="px-5 py-3">
              <p className="text-sm font-medium text-foreground">{m.title}</p>
              <p className="text-xs text-muted-foreground mt-0.5">{m.displayStatus}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default ActiveMaintenancesCard;
