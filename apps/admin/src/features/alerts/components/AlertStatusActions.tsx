import { CheckCheck } from "lucide-react";
import { StatusPill } from "@/components/StatusBadge";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { AlertDetail } from "@/lib/actions/alerts";

interface Props {
  alert: AlertDetail;
  isAcknowledging: boolean;
  onAcknowledge: () => void;
}

function AlertStatusActions(props: Props) {
  const { alert, isAcknowledging, onAcknowledge } = props;

  return (
    <>
      {!alert.resolvedAt && <StatusPill status={alert.impactAtFireTime} />}
      {alert.resolvedAt ? (
        <Badge variant="outline" className="px-3 py-1.5">Resolved</Badge>
      ) : (
        <Badge variant="destructive" className="px-3 py-1.5">Active</Badge>
      )}
      {alert.acknowledgedAt ? (
        <Badge className="gap-1.5 border-green-500/30 bg-green-500/10 px-3 py-1.5 text-green-600 dark:text-green-400">
          <CheckCheck size={13} />
          <span>Acked by <strong>{alert.acknowledgedBy}</strong></span>
        </Badge>
      ) : !alert.resolvedAt && (
        <Button variant="outline" onClick={onAcknowledge} disabled={isAcknowledging}>
          <CheckCheck size={13} />
          {isAcknowledging ? "…" : "Acknowledge"}
        </Button>
      )}
    </>
  );
}

export default AlertStatusActions;
