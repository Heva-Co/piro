import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { ConfigFieldSchema } from "@/lib/actions/checks";
import StringListControl from "./StringListControl";
import KeyValueControl from "./KeyValueControl";
import ObjectArrayControl from "./ObjectArrayControl";

interface Props {
  field: ConfigFieldSchema;
  value: unknown;
  onChange: (value: unknown) => void;
}

/** Dispatches a config field's schema to the right input control by its ConfigFieldType (RFC 0011). */
function FieldControl(props: Props) {
  const { field, value, onChange } = props;

  switch (field.type) {
    case "Enum":
      return (
        <Select value={asString(value)} onValueChange={(v) => v && onChange(v)}>
          <SelectTrigger className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(field.options ?? []).map((option) => (
              <SelectItem key={option} value={option}>{option}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      );

    case "Boolean":
      return (
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4 rounded border-input"
            checked={value === true}
            onChange={(e) => onChange(e.target.checked)}
          />
          {field.label}
        </label>
      );

    case "Number":
      return (
        <Input
          type="number"
          value={value == null ? "" : String(value)}
          onChange={(e) => onChange(e.target.value === "" ? null : Number(e.target.value))}
          placeholder={field.placeholder ?? undefined}
        />
      );

    case "Multiline":
    case "Code": // TODO(RFC 0010): swap for a CodeMirror editor once that dependency lands.
      return (
        <Textarea
          value={asString(value)}
          onChange={(e) => onChange(e.target.value)}
          rows={field.type === "Code" ? 14 : 6}
          className={field.type === "Code" ? "font-mono text-xs" : undefined}
          placeholder={field.placeholder ?? undefined}
        />
      );

    case "StringList":
      return <StringListControl value={value} onChange={onChange} placeholder={field.placeholder ?? undefined} />;

    case "KeyValue":
      return <KeyValueControl value={value} onChange={onChange} />;

    case "ObjectArray":
      return <ObjectArrayControl field={field} value={value} onChange={onChange} />;

    default: // String, Url, Email
      return (
        <Input
          type={field.type === "Url" ? "url" : field.type === "Email" ? "email" : "text"}
          value={asString(value)}
          onChange={(e) => onChange(e.target.value)}
          placeholder={field.placeholder ?? undefined}
        />
      );
  }
}

function asString(value: unknown): string {
  return value == null ? "" : String(value);
}

export default FieldControl;
