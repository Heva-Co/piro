import { Upload } from "lucide-react";

export function GoogleCloudConfig({
  serviceAccountJson,
  setServiceAccountJson,
}: {
  serviceAccountJson: string;
  setServiceAccountJson: (v: string) => void;
}) {
  function handleFileUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (ev) => {
      const text = ev.target?.result as string;
      try {
        setServiceAccountJson(JSON.stringify(JSON.parse(text), null, 2));
      } catch {
        setServiceAccountJson(text);
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
          <input
            type="file"
            accept=".json,application/json"
            className="hidden"
            onChange={handleFileUpload}
          />
        </label>
      </div>
      <p className="text-xs text-muted-foreground">
        Paste the contents of your Google Cloud service account key file (.json) or upload it
        directly. The key must have the necessary IAM permissions for the checks that use this
        integration (e.g. <code className="font-mono">run.executions.list</code> for Cloud Run Jobs).
      </p>
      <textarea
        value={serviceAccountJson}
        onChange={(e) => setServiceAccountJson(e.target.value)}
        rows={14}
        placeholder={'{\n  "type": "service_account",\n  "project_id": "my-project",\n  ...\n}'}
        className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full"
        required
      />
    </div>
  );
}
