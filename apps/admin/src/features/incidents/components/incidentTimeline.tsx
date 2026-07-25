import { Link } from "react-router-dom";
import {
  FlagTriangleRight,
  ArrowRightLeft,
  CheckCheck,
  PlusCircle,
  MinusCircle,
  Blend,
  Eye,
  EyeOff,
  AlertTriangle,
} from "lucide-react";
import { ROUTES } from "@/constants/routes";
import type { IncidentTimelineEvent } from "@/lib/actions/incidents";

// Icon per system (non-comment) timeline event type. Comment events render their own layout.
export const SYSTEM_EVENT_ICON: Record<string, React.ReactNode> = {
  Created: <FlagTriangleRight />,
  StatusChanged: <ArrowRightLeft />,
  Acknowledged: <CheckCheck />,
  ServiceAdded: <PlusCircle />,
  ServiceRemoved: <MinusCircle />,
  MergedTo: <Blend />,
  MergedFrom: <Blend />,
  Published: <Eye />,
  Unpublished: <EyeOff />,
  AlertFired: <AlertTriangle />,
};

// Human-readable description of a system timeline event, with links to related incidents/alerts.
export function describeSystemEvent(e: IncidentTimelineEvent): React.ReactNode {
  switch (e.type) {
    case "Created":
      return "Incident created";
    case "StatusChanged":
      return (
        <>
          Status changed from <strong>{e.oldStatus}</strong> to <strong>{e.newStatus}</strong>
        </>
      );
    case "Acknowledged":
      return (
        <>
          Acknowledged by <strong>{e.actorName}</strong>
        </>
      );
    case "ServiceAdded":
      return "Service added to incident";
    case "ServiceRemoved":
      return "Service removed from incident";
    case "MergedTo":
      return (
        <>
          Merged into incident{" "}
          <Link to={ROUTES.INCIDENTS.TIMELINE(e.relatedIncidentId!)} className="font-semibold underline hover:no-underline">
            #{e.relatedIncidentId}
          </Link>
        </>
      );
    case "MergedFrom":
      return (
        <>
          Absorbed incident{" "}
          <Link to={ROUTES.INCIDENTS.TIMELINE(e.relatedIncidentId!)} className="font-semibold underline hover:no-underline">
            #{e.relatedIncidentId}
          </Link>
        </>
      );
    case "Published":
      return "Published to status page";
    case "Unpublished":
      return "Unpublished from status page";
    case "AlertFired":
      return e.alertId != null ? (
        <>
          Alert attached{" "}
          <Link to={ROUTES.ALERTS.DETAIL(e.alertId)} className="font-semibold underline hover:no-underline">
            #{e.alertId}
          </Link>
        </>
      ) : (
        "Alert attached"
      );
    default:
      return e.type;
  }
}
