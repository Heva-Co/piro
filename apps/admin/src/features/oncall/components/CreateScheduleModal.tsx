import { TimezonePicker } from "@/components/TimezonePicker";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  name: string;
  onNameChange: (value: string) => void;
  description: string;
  onDescriptionChange: (value: string) => void;
  timeZone: string;
  onTimeZoneChange: (value: string) => void;
  error: string;
  isCreating: boolean;
  onSubmit: () => void;
}

function CreateScheduleModal(props: Props) {
  const {
    open, onOpenChange, name, onNameChange, description, onDescriptionChange,
    timeZone, onTimeZoneChange, error, isCreating, onSubmit,
  } = props;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>New On-Call Schedule</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-4">
          {error && <p className="text-xs text-destructive">{error}</p>}
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-foreground">Name</label>
            <Input
              autoFocus
              value={name}
              onChange={(e) => onNameChange(e.target.value)}
              placeholder="Production on-call"
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-foreground">Timezone</label>
            <TimezonePicker value={timeZone} onChange={onTimeZoneChange} />
            <p className="text-xs text-muted-foreground">
              Used to display shift times in the Gantt and for shift-start notifications. All data is stored in UTC — this only affects how times are shown.
            </p>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-foreground">
              Description <span className="text-muted-foreground font-normal">(optional)</span>
            </label>
            <Textarea
              value={description}
              onChange={(e) => onDescriptionChange(e.target.value)}
              placeholder="Who this schedule covers and when"
              rows={2}
              className="resize-none"
            />
          </div>
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="button" onClick={onSubmit} disabled={!name.trim() || isCreating}>
            {isCreating ? "Creating…" : "Create schedule"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default CreateScheduleModal;
