import { Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { ConfigFieldSchema } from "@/lib/actions/checks";
import DynamicConfigField from "./DynamicConfigField";

interface Props {
  field: ConfigFieldSchema;
  value: unknown;
  onChange: (value: unknown) => void;
}

/**
 * An add/remove list of nested objects — the control for an ObjectArray config field (RFC 0011).
 * Renders each item's fields recursively via DynamicConfigField using the field's itemSchema
 * (e.g. an HTTP check's ResponseRules).
 */
function ObjectArrayControl(props: Props) {
  const items = Array.isArray(props.value) ? (props.value as Record<string, unknown>[]) : [];
  const itemSchema = props.field.itemSchema ?? [];
  const set = (next: Record<string, unknown>[]) => props.onChange(next);

  return (
    <div className="flex flex-col gap-3">
      {items.map((item, i) => (
        <div key={i} className="flex flex-col gap-2 rounded-lg border p-3">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-muted-foreground">#{i + 1}</span>
            <Button type="button" variant="ghost" size="icon" onClick={() => set(items.filter((_, j) => j !== i))}>
              <Trash2 size={14} />
            </Button>
          </div>
          {itemSchema.map((sub) => (
            <DynamicConfigField
              key={sub.key}
              field={sub}
              value={item[sub.key]}
              onChange={(v) => set(items.map((it, j) => (j === i ? { ...it, [sub.key]: v } : it)))}
            />
          ))}
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => set([...items, {}])}>
        <Plus size={14} /> Add
      </Button>
    </div>
  );
}

export default ObjectArrayControl;
