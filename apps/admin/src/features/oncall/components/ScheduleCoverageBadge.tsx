import { AlertTriangle } from "lucide-react";

interface Props {
  hasCoverage: boolean;
}

// Header indicator for whether a schedule has any rotation layers covering it.
function ScheduleCoverageBadge(props: Props) {
  const { hasCoverage } = props;

  if (!hasCoverage) {
    return (
      <span className="inline-flex items-center gap-1.5 text-sm text-amber-600 dark:text-amber-500">
        <AlertTriangle size={13} />
        No coverage — add a rotation layer
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1.5 text-sm text-muted-foreground">
      <span className="w-2 h-2 rounded-full bg-green-500 inline-block" />
      Active
    </span>
  );
}

export default ScheduleCoverageBadge;
