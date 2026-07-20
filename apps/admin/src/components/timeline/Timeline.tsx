import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

interface Props {
  children: ReactNode;
  className?: string;
}

// Presentational vertical container for TimelineRow entries, shared across features. Holds no data or
// domain logic, just the shared spacing/rail so incident and postmortem timelines read as one system.
function Timeline(props: Props) {
  const { children, className } = props;
  return <div className={cn("flex flex-col gap-2", className)}>{children}</div>;
}

export default Timeline;
