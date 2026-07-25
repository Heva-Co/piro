import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { fmtRange, type ViewMode } from "../scheduleRange";

const VIEW_MODES: { label: string; value: ViewMode }[] = [
  { label: "1 Day", value: "1day" },
  { label: "1 Week", value: "1week" },
  { label: "2 Weeks", value: "2weeks" },
  { label: "1 Month", value: "1month" },
];

interface Props {
  from: Date;
  to: Date;
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
  onToday: () => void;
  onAdvance: (direction: 1 | -1) => void;
}

function ScheduleRangeToolbar(props: Props) {
  const { from, to, viewMode, onViewModeChange, onToday, onAdvance } = props;

  return (
    <div className="flex items-center gap-3 mt-6 mb-6 flex-wrap">
      <Button variant="outline" size="sm" onClick={onToday}>Today</Button>
      <div className="flex items-center gap-1">
        <Button variant="outline" size="icon" onClick={() => onAdvance(-1)}>
          <ChevronLeft size={14} />
        </Button>
        <Button variant="outline" size="icon" onClick={() => onAdvance(1)}>
          <ChevronRight size={14} />
        </Button>
      </div>
      <span className="font-semibold text-foreground text-sm">{fmtRange(from, to, viewMode)}</span>

      <div className="ml-auto flex items-center gap-1 rounded-lg border border-border p-0.5">
        {VIEW_MODES.map(({ label, value }) => (
          <button
            key={value}
            onClick={() => onViewModeChange(value)}
            className={`px-3 py-1 rounded-md text-sm transition-colors ${
              viewMode === value
                ? "bg-foreground text-background"
                : "text-muted-foreground hover:text-foreground"
            }`}
          >
            {label}
          </button>
        ))}
      </div>
    </div>
  );
}

export default ScheduleRangeToolbar;
