import { ExternalLink, Link2, PlusCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { AlertDetail } from "@/lib/actions/alerts";

interface Props {
  alert: AlertDetail;
  isCreating: boolean;
  onViewIncident: (incidentId: number) => void;
  onAttach: () => void;
  onCreate: () => void;
}

function AlertIncidentSection(props: Props) {
  const { alert, isCreating, onViewIncident, onAttach, onCreate } = props;

  if (alert.incidentId != null) {
    return (
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-semibold">Linked Incident</p>
          <p className="text-xs text-muted-foreground mt-0.5">{alert.incidentTitle ?? `Incident #${alert.incidentId}`}</p>
        </div>
        <Button variant="outline" onClick={() => onViewIncident(alert.incidentId!)}>
          <ExternalLink size={14} />
          View incident
        </Button>
      </div>
    );
  }

  return (
    <div className="flex items-center justify-between gap-4">
      <div>
        <p className="text-sm font-semibold">Not linked</p>
        <p className="text-xs text-muted-foreground mt-0.5">This alert isn't linked to any incident yet.</p>
      </div>
      <div className="flex items-center gap-2">
        <Button variant="outline" onClick={onAttach}>
          <Link2 size={14} />
          Attach to incident
        </Button>
        <Button onClick={onCreate} disabled={isCreating}>
          <PlusCircle size={14} />
          {isCreating ? "Creating…" : "Create incident"}
        </Button>
      </div>
    </div>
  );
}

export default AlertIncidentSection;
