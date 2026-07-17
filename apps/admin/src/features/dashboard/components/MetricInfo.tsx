import { HelpCircle } from "lucide-react";
import { Tooltip, TooltipTrigger, TooltipContent } from "@/components/ui/tooltip";

interface Props {
  /** Explanation of how this metric is generated and calculated. */
  text: string;
}

/** A small "?" icon that reveals, on hover/focus, how a dashboard metric is derived. */
function MetricInfo(props: Props) {
  const { text } = props;

  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <button
            type="button"
            aria-label="How is this calculated?"
            className="text-muted-foreground/50 hover:text-muted-foreground transition-colors"
          >
            <HelpCircle size={14} />
          </button>
        }
      />
      <TooltipContent className="max-w-xs text-xs leading-relaxed">{text}</TooltipContent>
    </Tooltip>
  );
}

export default MetricInfo;
