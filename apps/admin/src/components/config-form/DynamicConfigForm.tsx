import type { ConfigFieldSchema } from "@/lib/actions/checks";
import DynamicConfigField from "./DynamicConfigField";
import type { DynamicOptionsResolver } from "./DynamicOptionsSelect";

interface Props {
  /** The reflected field schema for the selected type (RFC 0011). */
  schema: ConfigFieldSchema[];
  /** Current structured config values, keyed by field.key. */
  values: Record<string, unknown>;
  /** Per-field validation errors, keyed by field.key. */
  errors?: Record<string, string>;
  /** Called with the full updated values map on any field change. */
  onChange: (values: Record<string, unknown>) => void;
  /** Optional resolver for `[DynamicOptions]` fields (RFC 0012) — supplied by hosts that support them (e.g. ActionDialog). */
  optionsResolver?: DynamicOptionsResolver;
}

/**
 * Renders a config form generically from a reflected `ConfigFieldSchema[]` — no per-type hand-written
 * component (RFC 0011). The value model is structured (`Record<string, unknown>`) so composite fields
 * (KeyValue, StringList, ObjectArray) keep their real shape. Conditional fields (`visibleWhen`) are
 * shown only when their controlling sibling holds a matching value.
 *
 * Persistence, general-settings fields, and any type-specific validation live in the caller
 * (CheckFormPage / the future integrations wrapper); this component only renders the config fields.
 */
function DynamicConfigForm(props: Props) {
  const { schema, values, errors, onChange, optionsResolver } = props;

  function isVisible(field: ConfigFieldSchema): boolean {
    if (!field.visibleWhen) return true;
    const current = values[field.visibleWhen.field];
    return field.visibleWhen.values.includes(String(current));
  }

  return (
    <div className="flex flex-col gap-4">
      {schema.filter(isVisible).map((field) => (
        <DynamicConfigField
          key={field.key}
          field={field}
          value={values[field.key]}
          error={errors?.[field.key]}
          onChange={(v) => onChange({ ...values, [field.key]: v })}
          optionsResolver={optionsResolver}
          dependsOnValue={
            field.optionsDependsOn ? asOptionalString(values[field.optionsDependsOn]) : undefined
          }
        />
      ))}
    </div>
  );
}

function asOptionalString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

export default DynamicConfigForm;
