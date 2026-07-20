import { useState } from "react";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { CreateFieldDefinitionRequest, PostmortemFieldType } from "@/lib/actions/postmortems";

interface Props {
  saving: boolean;
  onSubmit: (data: CreateFieldDefinitionRequest) => void;
}

const FIELD_TYPES: PostmortemFieldType[] = ["LongText", "Text", "Date", "Select"];

// Derives a stable snake_case key from the heading, matching the backend's key rules
// (^[a-z0-9]+(?:_[a-z0-9]+)*$).
function toKey(heading: string) {
  return heading
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function AddFieldDefinitionForm(props: Props) {
  const { saving, onSubmit } = props;
  const [heading, setHeading] = useState("");
  const [helpText, setHelpText] = useState("");
  const [fieldType, setFieldType] = useState<PostmortemFieldType>("LongText");

  function handleSubmit() {
    const key = toKey(heading);
    if (!key) return;
    onSubmit({ key, heading: heading.trim(), helpText: helpText.trim() || null, fieldType });
    setHeading("");
    setHelpText("");
    setFieldType("LongText");
  }

  return (
    <div className="flex flex-col gap-3 rounded-xl border bg-card p-5">
      <h2 className="text-sm font-semibold">Add a custom field</h2>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-medium text-muted-foreground">Heading</label>
          <Input
            value={heading}
            onChange={(e) => setHeading(e.target.value)}
            placeholder="Customer Communications"
          />
          {heading && (
            <span className="text-xs text-muted-foreground">
              Key: <code className="font-mono">{toKey(heading) || "—"}</code>
            </span>
          )}
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-medium text-muted-foreground">Type</label>
          <Select value={fieldType} onValueChange={(v) => v && setFieldType(v as PostmortemFieldType)}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {FIELD_TYPES.map((t) => (
                <SelectItem key={t} value={t}>
                  {t}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-xs font-medium text-muted-foreground">Help text (optional)</label>
        <Input
          value={helpText}
          onChange={(e) => setHelpText(e.target.value)}
          placeholder="What this section should capture"
        />
      </div>
      <div>
        <Button size="sm" onClick={handleSubmit} disabled={saving || !toKey(heading)}>
          <Plus size={13} /> {saving ? "Adding…" : "Add field"}
        </Button>
      </div>
    </div>
  );
}

export default AddFieldDefinitionForm;
