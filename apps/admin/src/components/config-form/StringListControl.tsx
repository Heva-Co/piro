import { Plus, Trash2 } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";

interface Props {
  value: unknown;
  onChange: (value: unknown) => void;
  placeholder?: string;
}

/** An add/remove list of strings — the control for a StringList config field (RFC 0011). */
 function StringListControl(props: Props) {
  const items = Array.isArray(props.value) ? (props.value as string[]) : [];
  const set = (next: string[]) => props.onChange(next);

  return (
    <div className="flex flex-col gap-2">
      {items.map((item, i) => (
        <div key={i} className="flex items-center gap-2">
          <Input
            value={item}
            placeholder={props.placeholder}
            onChange={(e) => set(items.map((v, j) => (j === i ? e.target.value : v)))}
          />
          <Button type="button" variant="ghost" size="icon" onClick={() => set(items.filter((_, j) => j !== i))}>
            <Trash2 size={14} />
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => set([...items, ""])}>
        <Plus size={14} /> Add
      </Button>
    </div>
  );
}

export default StringListControl;

