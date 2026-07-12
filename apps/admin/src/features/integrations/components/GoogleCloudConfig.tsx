import { useFormContext } from "react-hook-form";
import { Upload } from "lucide-react";
import { MASKED_SECRET_VALUE } from "@/constants/integrations";
import type { IntegrationFormValues } from "./types";

export function GoogleCloudConfig() {
  const { setValue, watch, formState: { errors } } = useFormContext<IntegrationFormValues>();
  const value = watch("serviceAccountJson");

  function handleFileUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (ev) => {
      const text = ev.target?.result as string;
      try {
        setValue("serviceAccountJson", JSON.stringify(JSON.parse(text), null, 2), { shouldValidate: true });
      } catch {
        setValue("serviceAccountJson", text, { shouldValidate: true });
      }
    };
    reader.readAsText(file);
    e.target.value = "";
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between">
        <label className="text-sm font-semibold">
          Service Account JSON <span className="text-destructive">*</span>
        </label>
        <label className="cursor-pointer flex items-center gap-1.5 rounded-lg border px-3 py-1 text-xs font-medium hover:bg-muted transition-colors">
          <Upload size={12} /> Upload .json
          <input type="file" accept=".json,application/json" className="hidden" onChange={handleFileUpload} />
        </label>
      </div>
      <p className="text-xs text-muted-foreground">
        Paste the contents of your Google Cloud service account key file (.json) or upload it directly.
        The key must have the necessary IAM permissions (e.g. <code className="font-mono">run.executions.list</code> for Cloud Run Jobs).
      </p>
      {value === MASKED_SECRET_VALUE && (
        <p className="text-xs text-amber-600 dark:text-amber-400">
          A key is already configured and hidden for security. Leave as-is to keep it, or paste/upload a new one to replace it.
        </p>
      )}
      <textarea
        value={value}
        onChange={(e) => setValue("serviceAccountJson", e.target.value, { shouldValidate: true })}
        rows={14}
        placeholder={'{\n  "type": "service_account",\n  "project_id": "my-project",\n  ...\n}'}
        className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full"
      />
      {errors.serviceAccountJson && <p className="text-xs text-destructive">{errors.serviceAccountJson.message}</p>}
    </div>
  );
}
