import { StatusBadge } from "@/components/StatusBadge";
import ListItemSkeleton from "@/features/dashboard/components/ListItemSkeleton";
import type { Incident } from "@/lib/actions/incidents";

interface Props {
  incidents: Incident[];
  isLoading: boolean;
}

function ActiveIncidentsCard(props: Props) {
  const { incidents, isLoading } = props;
  const active = incidents.filter((i) => i.status !== "Resolved");

  return (
    <div className="bg-card rounded-lg border border-border shadow-sm overflow-hidden">
      <div className="px-5 py-4 border-b border-border">
        <h2 className="font-semibold text-foreground">Active Incidents</h2>
      </div>
      {isLoading ? (
        <div className="divide-y divide-border">
          <ListItemSkeleton />
          <ListItemSkeleton />
        </div>
      ) : active.length === 0 ? (
        <div className="p-4 text-sm text-muted-foreground">No active incidents.</div>
      ) : (
        <ul className="divide-y divide-border">
          {active.map((incident) => (
            <li key={incident.id} className="px-5 py-3">
              <p className="text-sm font-medium text-foreground">{incident.title}</p>
              <div className="flex items-center gap-2 mt-1">
                <StatusBadge status={incident.status} />
                {incident.visibility !== "Public" && (
                  <span className="text-xs bg-yellow-100 text-yellow-700 rounded px-1.5 py-0.5">Private</span>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default ActiveIncidentsCard;
