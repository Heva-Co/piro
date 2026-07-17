import { Icon } from "@iconify/react";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { AlertSummary, AlertDetail } from "@/lib/actions/alerts";

interface Props {
  source: AlertSummary["source"] | AlertDetail["source"];
  sourceLabel?: AlertSummary["sourceLabel"] | AlertDetail["sourceLabel"];
  sourceIconifyIcon?: AlertSummary["sourceIconifyIcon"] | AlertDetail["sourceIconifyIcon"];
}

/** Badge shown for an alert that came from a third-party source (RFC 0001) instead of Piro's own checks. */
export function AlertSourceBadge(props: Props) {
  const { source, sourceLabel, sourceIconifyIcon } = props;
  if (source === "Internal") return null;

  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <span className="inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[10px] font-medium text-muted-foreground">
            {sourceIconifyIcon && <Icon icon={sourceIconifyIcon} className="size-3" />}
            External
          </span>
        }
      />
      <TooltipContent>Received via {sourceLabel ?? source}, not one of this Service's own checks.</TooltipContent>
    </Tooltip>
  );
}
