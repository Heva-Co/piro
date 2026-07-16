import { Upload } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { MASKED_SECRET_VALUE } from "@/constants/integrations";
import type { ConfigFieldSchema } from "@/lib/actions/integrations";

/** Visual-only stand-in shown in a masked password input — never sent to the server as-is. */
const MASKED_PASSWORD_PLACEHOLDER = "••••••••••••";

interface Props {
  field: ConfigFieldSchema;
  value: string;
  error?: string;
  onChange: (value: string) => void;
}

export function DynamicConfigField(props: Props) {
  const { field, value, error, onChange } = props;
  const isMasked = field.isSecret && value === MASKED_SECRET_VALUE;

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

      {field.type === "Enum" ? (
        <Select value={value} onValueChange={(v) => v && onChange(v)}>
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
          value={isMasked ? "" : value}
          onChange={(e) => onChange(e.target.value)}
          rows={12}
          placeholder={isMasked ? "Leave blank to keep the existing value…" : field.placeholder ?? undefined}
        />
      ) : (
        <Input
          type={field.isSecret ? "password" : field.type === "Url" ? "url" : field.type === "Email" ? "email" : "text"}
          value={isMasked ? "" : value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={isMasked ? MASKED_PASSWORD_PLACEHOLDER : field.placeholder ?? undefined}
        />
      )}

      {isMasked && (
        <p className="text-xs text-amber-600 dark:text-amber-400">
          A value is already configured and hidden for security. Leave blank to keep it, or enter a new one to replace it.
        </p>
      )}
      {!error && field.helpText && !isMasked && <p className="text-xs text-muted-foreground">{field.helpText}</p>}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
