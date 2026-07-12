import { Shield } from "lucide-react";
import { Tooltip, TooltipTrigger, TooltipContent } from "@/components/ui/tooltip";

interface ScheduleLabelProps {
  name: string;
  isPrimary: boolean;
}

export function ScheduleLabel(props: ScheduleLabelProps) {
  const { name, isPrimary } = props;
  if (isPrimary) return <>{name}</>;
  return (
    <Tooltip>
      <TooltipTrigger render={<span className="flex items-center gap-1 min-w-0" />}>
        <Shield size={12} className="shrink-0 text-muted-foreground" />
        <span className="truncate">{name}</span>
      </TooltipTrigger>
      <TooltipContent>
        You're a backup/secondary responder here — this schedule isn't the first escalation
        step for any service, so you won't be paged immediately.
      </TooltipContent>
    </Tooltip>
  );
}
