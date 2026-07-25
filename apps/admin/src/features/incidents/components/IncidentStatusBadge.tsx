import { Badge } from "@/components/ui/badge";

// Incident lifecycle status → badge color. Distinct from the service-status StatusPill
// (@/components/StatusBadge), which covers UP/DOWN/DEGRADED, not incident lifecycle.
const STATUS_COLORS: Record<string, string> = {
  INVESTIGATING: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  IDENTIFIED: "bg-orange-500/15 text-orange-600 dark:text-orange-400",
  MONITORING: "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  RESOLVED: "bg-green-500/15 text-green-600 dark:text-green-400",
  MERGED: "bg-violet-500/15 text-violet-600 dark:text-violet-400",
};

interface Props {
  status: string | null | undefined;
  className?: string;
}

function IncidentStatusBadge(props: Props) {
  const { status, className } = props;
  const color = STATUS_COLORS[(status ?? "").toUpperCase()] ?? "bg-muted text-muted-foreground";
  return <Badge className={`${color} ${className ?? ""}`}>{status}</Badge>;
}

export default IncidentStatusBadge;
