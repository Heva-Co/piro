import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";

interface Props {
  initialOccurredAt?: string;
  initialBody?: string;
  saving: boolean;
  submitLabel: string;
  onSubmit: (occurredAt: string, body: string) => void;
  onCancel?: () => void;
}

// datetime-local wants "yyyy-MM-ddThh:mm"; the API wants an ISO instant. Convert both ways here so the
// rest of the app keeps dealing in ISO strings.
function toLocalInput(iso?: string) {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function PostmortemAnnotationForm(props: Props) {
  const { initialOccurredAt, initialBody, saving, submitLabel, onSubmit, onCancel } = props;
  const [occurredAt, setOccurredAt] = useState(() => toLocalInput(initialOccurredAt));
  const [body, setBody] = useState(initialBody ?? "");

  function handleSubmit() {
    if (!occurredAt || !body.trim()) return;
    onSubmit(new Date(occurredAt).toISOString(), body.trim());
  }

  return (
    <div className="flex flex-col gap-2 rounded-lg border bg-muted/30 p-3">
      <div className="flex flex-col gap-1">
        <label className="text-xs font-medium text-muted-foreground">When</label>
        <Input
          type="datetime-local"
          value={occurredAt}
          onChange={(e) => setOccurredAt(e.target.value)}
        />
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
