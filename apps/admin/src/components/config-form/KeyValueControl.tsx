import { Plus, Trash2 } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";

interface Props {
  value: unknown;
  onChange: (value: unknown) => void;
}

/** An add/remove list of key/value pairs — the control for a KeyValue config field (RFC 0011). */
function KeyValueControl(props: Props) {
  const obj = isPlainObject(props.value) ? (props.value as Record<string, string>) : {};
  const entries = Object.entries(obj);
  const setEntries = (next: [string, string][]) =>
    props.onChange(Object.fromEntries(next.filter(([k]) => k !== "")));

  return (
    <div className="flex flex-col gap-2">
      {entries.map(([k, v], i) => (
        <div key={i} className="flex items-center gap-2">
          <Input
            value={k}
            placeholder="Key"
            className="w-1/3"
            onChange={(e) => setEntries(entries.map((pair, j) => (j === i ? [e.target.value, pair[1]] : pair)))}
          />
          <Input
            value={v}
            placeholder="Value"
            onChange={(e) => setEntries(entries.map((pair, j) => (j === i ? [pair[0], e.target.value] : pair)))}
          />
          <Button type="button" variant="ghost" size="icon" onClick={() => setEntries(entries.filter((_, j) => j !== i))}>
            <Trash2 size={14} />
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => setEntries([...entries, ["", ""]])}>
        <Plus size={14} /> Add
      </Button>
    </div>
  );
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export default KeyValueControl;
