import { Label } from "@/components/ui/label";
import type { ConfigFieldSchema } from "@/lib/actions/checks";
import FieldControl from "./FieldControl";
import DynamicOptionsSelect, { type DynamicOptionsResolver } from "./DynamicOptionsSelect";

interface Props {
  field: ConfigFieldSchema;
  value: unknown;
  error?: string;
  onChange: (value: unknown) => void;
  /** Resolver for `[DynamicOptions]` fields (RFC 0012), if the host supports them. */
  optionsResolver?: DynamicOptionsResolver;
  /** Current value of this field's cascade parent (field.optionsDependsOn), if any. */
  dependsOnValue?: string;
}

/**
 * Renders one config field from its reflected schema (RFC 0011): a label (except Boolean, which
 * carries its own inline label), the type-appropriate control, and help/error text. A field marked
 * with a dynamic options source (RFC 0012) renders a runtime-populated select when the host provided a
 * resolver. Value is structured (`unknown`) so composite types keep their real shape.
 */
function DynamicConfigField(props: Props) {
  const { field, value, error, onChange, optionsResolver, dependsOnValue } = props;

  const useDynamicOptions = Boolean(field.optionsSource) && Boolean(optionsResolver);

  return (
    <div className="flex flex-col gap-1.5">
      {field.type !== "Boolean" && (
        <Label>
          {field.label} {field.required && <span className="text-destructive">*</span>}
        </Label>
      )}

      {useDynamicOptions ? (
        <DynamicOptionsSelect
          field={field}
          value={value}
          onChange={onChange}
          resolver={optionsResolver!}
          dependsOnValue={dependsOnValue}
        />
      ) : (
        <FieldControl field={field} value={value} onChange={onChange} />
      )}

      {!error && field.helpText && <p className="text-xs text-muted-foreground">{field.helpText}</p>}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}

export default DynamicConfigField;
