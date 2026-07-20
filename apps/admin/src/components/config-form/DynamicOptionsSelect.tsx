import { useQuery } from "@tanstack/react-query";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { ConfigFieldSchema } from "@/lib/actions/checks";

/** Resolves runtime options for a dynamic-options field (RFC 0012). Supplied by the form's host. */
export type DynamicOptionsResolver = (
  sourceKey: string,
  dependsOnValue: string | undefined,
) => Promise<{ value: string; label: string }[]>;

interface Props {
  field: ConfigFieldSchema;
  value: unknown;
  onChange: (value: unknown) => void;
  resolver: DynamicOptionsResolver;
  /** Current value of the field this one cascades from (field.optionsDependsOn), if any. */
  dependsOnValue?: string;
}

/**
 * A select whose options are fetched at runtime from the connected integration (RFC 0012), for a field
 * marked `[DynamicOptions]`. Re-fetches when its cascade parent changes; disabled until that parent has
 * a value (e.g. issue type waits for a project). Purely presentational otherwise — the actual fetch is
 * the host-provided resolver, so this component knows nothing about which integration it belongs to.
 */
function DynamicOptionsSelect(props: Props) {
  const { field, value, onChange, resolver, dependsOnValue } = props;

  const waitingOnParent = Boolean(field.optionsDependsOn) && !dependsOnValue;

  const { data: options = [], isLoading } = useQuery({
    queryKey: ["field-options", field.optionsSource, dependsOnValue ?? null],
    queryFn: () => resolver(field.optionsSource!, dependsOnValue),
    enabled: Boolean(field.optionsSource) && !waitingOnParent,
  });

  const placeholder = waitingOnParent
    ? "Select the field above first"
    : isLoading
      ? "Loading…"
      : (field.placeholder ?? "Select…");

  return (
    <Select
      value={typeof value === "string" ? value : ""}
      onValueChange={(v) => v && onChange(v)}
      disabled={waitingOnParent || isLoading}
    >
      <SelectTrigger className="w-full">
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

export default DynamicOptionsSelect;
