import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { DateTimePicker } from "@/components/DateTimePicker";

interface Props {
  initialOccurredAt?: string;
  initialBody?: string;
  saving: boolean;
  submitLabel: string;
  onSubmit: (occurredAt: string, body: string) => void;
  onCancel?: () => void;
}

// DateTimePicker and the API both speak UTC ISO strings, so no conversion is needed here.
function PostmortemAnnotationForm(props: Props) {
  const { initialOccurredAt, initialBody, saving, submitLabel, onSubmit, onCancel } = props;
  const [occurredAt, setOccurredAt] = useState(initialOccurredAt ?? "");
  const [body, setBody] = useState(initialBody ?? "");

  function handleSubmit() {
    if (!occurredAt || !body.trim()) return;
    onSubmit(occurredAt, body.trim());
  }

  return (
    <div className="flex flex-col gap-2 rounded-lg border bg-muted/30 p-3">
      <div className="flex flex-col gap-1">
        <label className="text-xs font-medium text-muted-foreground">When</label>
        <DateTimePicker value={occurredAt} onChange={setOccurredAt} placeholder="Pick a date and time" />
      </div>
      <div className="flex flex-col gap-1">
        <label className="text-xs font-medium text-muted-foreground">Note</label>
        <Textarea
          rows={2}
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder="Vendor confirmed the outage at 14:32…"
        />
      </div>
      <div className="flex items-center gap-2">
        <Button size="sm" onClick={handleSubmit} disabled={saving || !occurredAt || !body.trim()}>
          {saving ? "Saving…" : submitLabel}
        </Button>
        {onCancel && (
          <Button size="sm" variant="outline" onClick={onCancel} disabled={saving}>
            Cancel
          </Button>
        )}
      </div>
    </div>
  );
}

export default PostmortemAnnotationForm;
