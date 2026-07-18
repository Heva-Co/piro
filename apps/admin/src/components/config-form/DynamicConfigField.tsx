import { Label } from "@/components/ui/label";
import type { ConfigFieldSchema } from "@/lib/actions/checks";
import FieldControl from "./FieldControl";

interface Props {
  field: ConfigFieldSchema;
  value: unknown;
  error?: string;
  onChange: (value: unknown) => void;
}

/**
 * Renders one config field from its reflected schema (RFC 0011): a label (except Boolean, which
 * carries its own inline label), the type-appropriate control, and help/error text. Value is
 * structured (`unknown`) so composite types (StringList, KeyValue, ObjectArray) keep their real
 * shape. Integration-only concerns (secret masking, generated fields, file upload) are NOT handled
 * here — they live in the integrations-specific wrapper.
 */
function DynamicConfigField(props: Props) {
  const { field, value, error, onChange } = props;

  return (
    <div className="flex flex-col gap-1.5">
      {field.type !== "Boolean" && (
        <Label>
          {field.label} {field.required && <span className="text-destructive">*</span>}
        </Label>
      )}

      <FieldControl field={field} value={value} onChange={onChange} />

      {!error && field.helpText && <p className="text-xs text-muted-foreground">{field.helpText}</p>}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}

export default DynamicConfigField;
