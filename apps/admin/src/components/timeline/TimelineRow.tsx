import type { ReactNode } from "react";
import { Marker, MarkerContent, MarkerIcon } from "@/components/ui/marker";
import { cn } from "@/lib/utils";

interface Props {
  icon: ReactNode;
  /** Short, muted lead line (timestamp, source, actor). */
  meta: ReactNode;
  /** The main body of the entry. Optional for one-line system events. */
  children?: ReactNode;
  /** Right-aligned controls (edit/delete), shown on the row. */
  actions?: ReactNode;
  className?: string;
}

// Presentational timeline entry shared across features (incidents, postmortems). It owns only the
// dot + line + layout chrome via the Marker primitive; each feature supplies its own icon, meta line,
// body, and actions, so every timeline keeps its own UI while reading as one system.
function TimelineRow(props: Props) {
  const { icon, meta, children, actions, className } = props;
  return (
    <Marker variant="separator" className={cn("items-start", className)}>
      <MarkerIcon>{icon}</MarkerIcon>
      <MarkerContent className="flex flex-1 flex-col gap-0.5 text-left">
        <span className="text-xs text-muted-foreground">{meta}</span>
        {children && <div className="text-sm">{children}</div>}
      </MarkerContent>
      {actions && <div className="flex shrink-0 items-start gap-1">{actions}</div>}
    </Marker>
  );
}

export default TimelineRow;
