import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "@/components/AdminLayout";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { integrationsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const inp = "rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";

const INTEGRATION_TYPES = [{ value: "GoogleCloud", label: "Google Cloud" }];

// ── Type-specific config panels ────────────────────────────────────────────────

function GoogleCloudConfig({
  serviceAccountJson,
  setServiceAccountJson,
}: {
  serviceAccountJson: string;
  setServiceAccountJson: (v: string) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-sm font-semibold">
        Service Account JSON <span className="text-destructive">*</span>
      </label>
      <p className="text-xs text-muted-foreground">
        Paste the contents of your Google Cloud service account key file (.json). The key must have
        the necessary IAM permissions for the checks that use this integration (e.g.{" "}
        <code className="font-mono">run.executions.list</code> for Cloud Run Jobs).
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

// ── Page ───────────────────────────────────────────────────────────────────────

export default function IntegrationFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [type, setType] = useState("GoogleCloud");
  const [serviceAccountJson, setServiceAccountJson] = useState("");
  const [error, setError] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState("");

  const { data: existing } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION(id!),
    queryFn: () => integrationsApi.get(Number(id)),
    enabled: isEdit,
  });

  useEffect(() => {
    if (!existing) return;
    setName(existing.name);
    setDescription(existing.description ?? "");
    setType(existing.type);
    try {
      const config = JSON.parse(existing.configJson);
      setServiceAccountJson(
        config.serviceAccountJson
          ? JSON.stringify(JSON.parse(config.serviceAccountJson), null, 2)
          : ""
      );
    } catch {
      setServiceAccountJson("");
    }
  }, [existing]);

  function buildConfigJson(): string {
    switch (type) {
      case "GoogleCloud":
        return JSON.stringify({ serviceAccountJson: serviceAccountJson.trim() });
      default:
        return "{}";
    }
  }

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload = {
        name,
        type,
        description: description || undefined,
        configJson: buildConfigJson(),
      };
      if (isEdit) return integrationsApi.update(Number(id), payload);
      return integrationsApi.create(payload);
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS });
      if (!isEdit && data && "id" in (data as object)) {
        navigate(ROUTES.INTEGRATIONS.DETAIL((data as { id: number }).id));
      }
    },
    onError: () => setError("Failed to save integration."),
  });

  const deleteMutation = useMutation({
    mutationFn: () => integrationsApi.delete(Number(id)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS });
      navigate(ROUTES.INTEGRATIONS.LIST);
    },
    onError: (err: unknown) => {
      const msg =
        err instanceof Error ? err.message : "Failed to delete integration.";
      if (msg.includes("409") || msg.toLowerCase().includes("conflict")) {
        setError(
          "This integration is still referenced by one or more checks. Remove those checks first."
        );
      } else {
        setError(msg);
      }
    },
  });

  const pageTitle = isEdit ? (existing?.name ?? "Edit Integration") : "New Integration";

  return (
    <AdminLayout title={pageTitle}>
      <div className="flex flex-col gap-6">
        {/* Breadcrumb */}
        <nav className="flex items-center gap-2 text-sm text-muted-foreground">
          <button
            onClick={() => navigate(ROUTES.INTEGRATIONS.LIST)}
            className="hover:text-foreground transition-colors"
          >
            Integrations
          </button>
          <span>/</span>
          <span className="text-foreground font-medium">{pageTitle}</span>
        </nav>

        {/* Title */}
        <div>
          <h1 className="text-xl font-bold">{pageTitle}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Shared provider credentials reused across multiple checks.
          </p>
        </div>

        {error && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            {error}
          </div>
        )}

        {/* Main card */}
        <div className="rounded-xl border bg-card">
          {/* Integration type */}
          <div className="px-6 pt-6 pb-4 border-b border-border">
            <p className="text-sm font-semibold mb-1">Provider</p>
            <p className="text-xs text-muted-foreground mb-3">
              The cloud provider this integration connects to
            </p>
            <Select value={type} onValueChange={(v) => v && setType(v)} disabled={isEdit}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {INTEGRATION_TYPES.map((t) => (
                  <SelectItem key={t.value} value={t.value}>
                    {t.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {isEdit && (
              <p className="text-xs text-muted-foreground mt-1.5">
                Provider cannot be changed after creation.
              </p>
            )}
          </div>

          {/* Common fields + type-specific */}
          <div className="px-6 py-6 flex flex-col gap-5">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">
                Name <span className="text-destructive">*</span>
              </label>
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Production GCP"
                className={inp}
                required
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">Description</label>
              <input
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional description"
                className={inp}
              />
            </div>

            {type === "GoogleCloud" && (
              <GoogleCloudConfig
                serviceAccountJson={serviceAccountJson}
                setServiceAccountJson={setServiceAccountJson}
              />
            )}
          </div>

          {/* Footer */}
          <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-border">
            <button
              type="button"
              onClick={() => navigate(ROUTES.INTEGRATIONS.LIST)}
              className="rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted transition-colors"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={() => {
                setError("");
                saveMutation.mutate();
              }}
              disabled={saveMutation.isPending || !name || (type === "GoogleCloud" && !serviceAccountJson.trim())}
              className="rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
            >
              {saveMutation.isPending ? "Saving…" : isEdit ? "Save changes" : "Create Integration"}
            </button>
          </div>
        </div>

        {/* Danger Zone */}
        {isEdit && (
          <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6 flex flex-col gap-4">
            <p className="text-sm">
              Permanently delete this integration. Type{" "}
              <code className="font-mono font-semibold">{existing?.name}</code> to confirm.
            </p>
            <p className="text-xs text-muted-foreground">
              Deletion is blocked if any checks are still referencing this integration.
            </p>
            <div className="flex items-center gap-3">
              <input
                value={deleteConfirm}
                onChange={(e) => setDeleteConfirm(e.target.value)}
                placeholder={existing?.name ?? ""}
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-destructive w-64"
              />
              <button
                type="button"
                disabled={deleteConfirm !== existing?.name || deleteMutation.isPending}
                onClick={() => deleteMutation.mutate()}
                className="rounded-lg bg-destructive text-destructive-foreground px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-40 transition-opacity"
              >
                {deleteMutation.isPending ? "Deleting…" : "Delete Integration"}
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
