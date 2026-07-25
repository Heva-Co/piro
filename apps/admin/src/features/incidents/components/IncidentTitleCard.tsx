import { useFormattedDate } from "@/hooks/useFormattedDate";

interface Props {
  title: string;
  onTitleChange: (value: string) => void;
  onCommit: () => void;
  disabled: boolean;
  startDateTime: number;
  endDateTime?: number | null;
}

function IncidentTitleCard(props: Props) {
  const { title, onTitleChange, onCommit, disabled, startDateTime, endDateTime } = props;
  const { formatTimestamp } = useFormattedDate();

  return (
    <div className="rounded-xl border border-border bg-card px-6 py-5 mb-4">
      <input
        type="text"
        value={title}
        onChange={(e) => onTitleChange(e.target.value)}
        onBlur={onCommit}
        disabled={disabled}
        className="text-xl font-bold bg-transparent border-0 border-b border-transparent hover:border-border focus:border-foreground/40 focus:outline-none w-full transition-colors disabled:hover:border-transparent"
      />
      <div className="flex items-center gap-2 mt-2 flex-wrap text-xs text-muted-foreground">
        <span>Started {formatTimestamp(startDateTime)}</span>
        {endDateTime && <span>· Resolved {formatTimestamp(endDateTime)}</span>}
      </div>
    </div>
  );
}

export default IncidentTitleCard;
