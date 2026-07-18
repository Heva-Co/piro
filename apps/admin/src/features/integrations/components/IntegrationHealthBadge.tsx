import { CheckCircle2, AlertTriangle, CircleCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { IntegrationHealthResult } from "../utils/integrationHealth";

interface Props {
  health: IntegrationHealthResult;
}

/**
 * Compact status pill for an integration row: green "Configured" when every required secret is
 * stored, amber "Needs setup" (with the missing secrets in a tooltip) when one is blank, and a
 * neutral "Ready" for a type with no user-supplied required secret (so the cell is never empty).
 * OAuth types are handled separately by the row (IntegrationOAuthStatusBadge) and never reach here.
 */
function IntegrationHealthBadge(props: Props) {
  const { health } = props;

  if (health.status === "unknown") return null;

  if (health.status === "ready") {
    return (
      <Badge
        variant="outline"
        className="border-emerald-500/30 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
      >
        <CircleCheck />
        Ready
      </Badge>
    );
  }

  if (health.status === "configured") {
    return (
      <Badge
        variant="outline"
        className="border-emerald-500/30 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
      >
        <CheckCircle2 />
        Configured
      </Badge>
    );
  }

  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <Badge
            variant="outline"
            className="cursor-default border-amber-500/30 bg-amber-500/10 text-amber-600 dark:text-amber-400"
          >
            <AlertTriangle />
            Needs setup
          </Badge>
        }
      />
      <TooltipContent>
        Missing secret{health.missingSecrets.length > 1 ? "s" : ""}: {health.missingSecrets.join(", ")}
      </TooltipContent>
    </Tooltip>
  );
}

export default IntegrationHealthBadge;
