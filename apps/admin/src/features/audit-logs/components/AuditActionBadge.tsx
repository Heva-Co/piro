import { Badge } from "@/components/ui/badge";
import type { AuditAction } from "@/lib/actions/audit-logs";

interface Props {
  action: AuditAction;
}

function variantFor(action: AuditAction): "default" | "destructive" | "secondary" | "outline" {
  switch (action) {
    case "Create":
      return "default";
    case "Delete":
    case "LoginFailed":
      return "destructive";
    case "Update":
      return "secondary";
    default:
      // Login and Logout: routine, and not a change to anything.
      return "outline";
  }
}

function AuditActionBadge(props: Props) {
  const { action } = props;

  return <Badge variant={variantFor(action)}>{action}</Badge>;
}

export default AuditActionBadge;
