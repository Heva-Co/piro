import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import type { Postmortem, PostmortemFieldValueUpdate } from "@/lib/actions/postmortems";

interface Props {
  postmortem: Postmortem;
  saving: boolean;
  onSave: (fields: PostmortemFieldValueUpdate[]) => void;
}

// Renders each analysis section from its field definition (heading, help text, type) and lets the
// author edit the value. LongText → textarea, everything else → single-line input for now.
// The parent keys this component by the postmortem's updatedAt, so a save remounts it and the
// lazy initial state re-hydrates from the freshly-saved values — no prop-sync effect needed.
function PostmortemFieldsSection(props: Props) {
  const { postmortem, saving, onSave } = props;
  const [values, setValues] = useState<Record<number, string>>(() => {
    const next: Record<number, string> = {};
    for (const f of postmortem.fields) next[f.fieldDefinitionId] = f.value;
    return next;
  });

  const dirty = postmortem.fields.some((f) => (values[f.fieldDefinitionId] ?? "") !== f.value);

  function handleSave() {
    const fields = postmortem.fields.map((f) => ({
      fieldDefinitionId: f.fieldDefinitionId,
      value: values[f.fieldDefinitionId] ?? "",
    }));
    onSave(fields);
  }

  return (
    <div className="rounded-xl border bg-card">
      <div className="flex items-center justify-between border-b px-5 py-3">
        <h2 className="text-sm font-semibold">Analysis</h2>
        <Button size="sm" onClick={handleSave} disabled={saving || !dirty}>
          {saving ? "Saving…" : "Save analysis"}
        </Button>
      </div>

      <div className="flex flex-col gap-6 p-5">
        {postmortem.fields.map((f) => (
          <div key={f.fieldDefinitionId} className="flex flex-col gap-1.5">
            <label className="text-sm font-semibold">{f.heading}</label>
            {f.helpText && <p className="text-xs text-muted-foreground">{f.helpText}</p>}
            {f.fieldType === "LongText" ? (
              <Textarea
                rows={4}
                value={values[f.fieldDefinitionId] ?? ""}
                onChange={(e) =>
                  setValues((v) => ({ ...v, [f.fieldDefinitionId]: e.target.value }))
                }
              />
            ) : (
              <Input
                type={f.fieldType === "Date" ? "date" : "text"}
                value={values[f.fieldDefinitionId] ?? ""}
                onChange={(e) =>
                  setValues((v) => ({ ...v, [f.fieldDefinitionId]: e.target.value }))
                }
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

export default PostmortemFieldsSection;
