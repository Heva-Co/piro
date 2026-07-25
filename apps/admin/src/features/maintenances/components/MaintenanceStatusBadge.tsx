import { Badge } from "@/components/ui/badge";

// Maintenance display status → badge color. Shared by the list and detail pages
// so the color map lives in one place.
const STATUS_COLORS: Record<string, string> = {
  Active: "bg-green-500/15 text-green-600 dark:text-green-400",
  Scheduled: "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  Completed: "bg-indigo-500/15 text-indigo-600 dark:text-indigo-400",
  Cancelled: "bg-muted text-muted-foreground",
};

interface Props {
  status: string;
  className?: string;
}

function MaintenanceStatusBadge(props: Props) {
  const { status, className } = props;
  const color = STATUS_COLORS[status] ?? "bg-muted text-muted-foreground";
  return <Badge className={`${color} ${className ?? ""}`}>{status}</Badge>;
}

export default MaintenanceStatusBadge;
