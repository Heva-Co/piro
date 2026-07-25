import { CheckCheck, Globe, Lock } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { Incident } from "@/lib/actions/incidents";
import IncidentStatusBadge from "./IncidentStatusBadge";

interface Props {
  incident: Incident;
  isResolved: boolean;
  isAcknowledging: boolean;
  onAcknowledge: () => void;
}

function IncidentStatusActions(props: Props) {
  const { incident, isResolved, isAcknowledging, onAcknowledge } = props;
  const isPublic = incident.visibility === "Public";

  return (
    <>
      <IncidentStatusBadge status={incident.status} />
      {isPublic ? (
        <span className="flex items-center gap-1 text-xs text-green-600 dark:text-green-400">
          <Globe size={12} /> Public
        </span>
      ) : (
        <span className="flex items-center gap-1 text-xs text-muted-foreground">
          <Lock size={12} /> Private
        </span>
      )}
      {!isResolved && (
        incident.acknowledgedAt ? (
          <Badge className="gap-1.5 border-green-500/30 bg-green-500/10 px-3 py-1.5 text-green-600 dark:text-green-400">
            <CheckCheck size={13} />
            <span>Acked by <strong>{incident.acknowledgedBy}</strong></span>
          </Badge>
        ) : (
          <Button onClick={onAcknowledge} disabled={isAcknowledging} variant="outline">
            <CheckCheck size={13} />
            {isAcknowledging ? "…" : "Acknowledge"}
          </Button>
        )
      )}
    </>
  );
}

export default IncidentStatusActions;
