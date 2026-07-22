import { Upload } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { MASKED_SECRET_VALUE } from "@/constants/integrations";
import type { ConfigFieldSchema } from "@/lib/actions/integrations";
import { GeneratedConfigField } from "./GeneratedConfigField";
import DynamicOptionsSelect, { type DynamicOptionsResolver } from "@/components/config-form/DynamicOptionsSelect";
import KeyValueControl from "@/components/config-form/KeyValueControl";

/** Visual-only stand-in shown in a masked password input — never sent to the server as-is. */
const MASKED_PASSWORD_PLACEHOLDER = "••••••••••••";

interface Props {
  field: ConfigFieldSchema;
  value: unknown;
  error?: string;
  onChange: (value: unknown) => void;
  /** True while creating a new Integration — a generated field has no value yet to show. */
  isCreating: boolean;
  /** Regenerates this field's value server-side — undefined while creating (nothing to regenerate yet). */
  onRegenerate?: () => void;
  isRegenerating?: boolean;
  /**
   * Resolver for `[DynamicOptions]` fields (RFC 0012), provided only when the integration is
   * OAuth-connected (edit mode with a live token). When absent, such a field falls back to free text —
   * options can't be listed without a connection.
   */
  optionsResolver?: DynamicOptionsResolver;
  /** Current value of this field's cascade parent (field.optionsDependsOn), if any. */
  dependsOnValue?: string;
}

export function DynamicConfigField(props: Props) {
  const { field, value, error, onChange, isCreating, onRegenerate, isRegenerating, optionsResolver, dependsOnValue } = props;
  // Every field but KeyValue is a scalar rendered into a text control, so coerce to a string for those
  // branches; the KeyValue branch consumes the raw object value instead.
  const stringValue = typeof value === "string" ? value : "";
  const isMasked = field.isSecret && stringValue === MASKED_SECRET_VALUE;
  const useDynamicOptions = Boolean(field.optionsSource) && Boolean(optionsResolver);

  if (field.isGenerated)
    return (
      <GeneratedConfigField
        field={field}
        value={stringValue}
        isCreating={isCreating}
        onRegenerate={onRegenerate}
        isRegenerating={isRegenerating}
      />
    );

  function handleFileUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (ev) => {
      const text = ev.target?.result as string;
      try {
        onChange(JSON.stringify(JSON.parse(text), null, 2));
      } catch {
        onChange(text);
      }
    };
    reader.readAsText(file);
    e.target.value = "";
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between">
        <Label>
          {field.label} {field.required && <span className="text-destructive">*</span>}
        </Label>
        {field.supportsFileUpload && (
          <label className="flex cursor-pointer items-center gap-1.5 rounded-lg border px-3 py-1 text-xs font-medium transition-colors hover:bg-muted">
            <Upload size={12} /> Upload .json
            <input type="file" accept=".json,application/json" className="hidden" onChange={handleFileUpload} />
          </label>
        )}
      </div>

      {useDynamicOptions ? (
        <DynamicOptionsSelect
          field={field}
          value={stringValue}
          onChange={(v) => onChange(typeof v === "string" ? v : "")}
          resolver={optionsResolver!}
          dependsOnValue={dependsOnValue}
        />
      ) : field.type === "Enum" ? (
        <Select value={stringValue} onValueChange={(v) => v && onChange(v)}>
          <SelectTrigger className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(field.options ?? []).map((option) => (
              <SelectItem key={option} value={option}>{option}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      ) : field.type === "Multiline" ? (
        <Textarea
          value={isMasked ? "" : stringValue}
          onChange={(e) => onChange(e.target.value)}
          rows={12}
          placeholder={isMasked ? "Leave blank to keep the existing value…" : field.placeholder ?? undefined}
        />
      ) : field.type === "KeyValue" ? (
        <KeyValueControl value={value} onChange={onChange} />
      ) : (
        <Input
          type={field.isSecret ? "password" : field.type === "Url" ? "url" : field.type === "Email" ? "email" : "text"}
          value={isMasked ? "" : stringValue}
          onChange={(e) => onChange(e.target.value)}
          placeholder={isMasked ? MASKED_PASSWORD_PLACEHOLDER : field.placeholder ?? undefined}
        />
      )}

      {isMasked && (
        <p className="text-xs text-muted-foreground">
          A value is already configured and hidden for security. Leave blank to keep it, or enter a new one to replace it.
        </p>
      )}
      {!error && field.helpText && !isMasked && <p className="text-xs text-muted-foreground">{field.helpText}</p>}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
