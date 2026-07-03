import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, Controller } from "react-hook-form";
import { Icon } from "@iconify/react";
import { AdminLayout } from "@/components/AdminLayout";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { integrationsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { INTEGRATION_TYPE_MAP, INTEGRATION_TYPES } from "@/constants/integrations";
import { GoogleCloudConfig } from "../components/GoogleCloudConfig";
import { JiraConfig } from "../components/JiraConfig";

interface FormValues {
  name: string;
  description: string;
  type: string;
  // GoogleCloud
  serviceAccountJson: string;
  // Jira
  jiraBaseUrl: string;
  jiraEmail: string;
  jiraApiToken: string;
  jiraProjectKey: string;
  jiraIssueType: string;
}

export default function IntegrationFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const qc = useQueryClient();

  const {
    register,
    handleSubmit,
    control,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    defaultValues: {
      name: "",
      description: "",
      type: "GoogleCloud",
      serviceAccountJson: "",
      jiraBaseUrl: "",
      jiraEmail: "",
      jiraApiToken: "",
      jiraProjectKey: "",
      jiraIssueType: "",
    },
  });

  const type = watch("type");
  const [deleteConfirm, setDeleteConfirm] = useState("");

  const { data: existing } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION(id!),
    queryFn: () => integrationsApi.get(Number(id)),
    enabled: isEdit,
  });

  useEffect(() => {
    if (!existing) return;
    const base: Partial<FormValues> = {
      name: existing.name,
      description: existing.description ?? "",
      type: existing.type,
    };
    try {
      const config = JSON.parse(existing.configJson);
      if (existing.type === "GoogleCloud") {
        base.serviceAccountJson = config.serviceAccountJson
          ? JSON.stringify(JSON.parse(config.serviceAccountJson), null, 2)
          : "";
      } else if (existing.type === "Jira") {
        base.jiraBaseUrl    = config.baseUrl    ?? "";
        base.jiraEmail      = config.email      ?? "";
        base.jiraApiToken   = config.apiToken   ?? "";
        base.jiraProjectKey = config.projectKey ?? "";
        base.jiraIssueType  = config.issueType  ?? "";
      }
    } catch { /* ignore */ }
    reset(base as FormValues);
  }, [existing, reset]);

  function buildConfigJson(values: FormValues): string {
    switch (values.type) {
      case "GoogleCloud":
        return JSON.stringify({ serviceAccountJson: values.serviceAccountJson.trim() });
      case "Jira":
        return JSON.stringify({
          baseUrl:    values.jiraBaseUrl.trim(),
          email:      values.jiraEmail.trim(),
          apiToken:   values.jiraApiToken.trim(),
          projectKey: values.jiraProjectKey.trim(),
          issueType:  values.jiraIssueType.trim(),
        });
      default:
        return "{}";
    }
  }

  const saveMutation = useMutation({
    mutationFn: (values: FormValues) => {
      const payload = {
        name: values.name,
        type: values.type,
        description: values.description || undefined,
        configJson: buildConfigJson(values),
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
  });

  const deleteMutation = useMutation({
    mutationFn: () => integrationsApi.delete(Number(id)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS });
      navigate(ROUTES.INTEGRATIONS.LIST);
    },
  });

  const inp = (hasError: boolean) =>
    `rounded-lg border ${hasError ? "border-destructive" : "border-border"} bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full`;

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

        {saveMutation.isError && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            Failed to save integration.
          </div>
        )}

        <form onSubmit={handleSubmit((values) => saveMutation.mutateAsync(values))}>
          <div className="rounded-xl border bg-card">
            {/* Provider */}
            <div className="px-6 pt-6 pb-4 border-b border-border">
              <p className="text-sm font-semibold mb-1">Provider</p>
              <p className="text-xs text-muted-foreground mb-3">
                The cloud provider this integration connects to
              </p>
              <Controller
                control={control}
                name="type"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={(v) => v && field.onChange(v)} disabled={isEdit}>
                    <SelectTrigger className="w-48">
                      <SelectValue>
                        <span className="inline-flex items-center gap-2">
                          {(() => {
                            const meta = INTEGRATION_TYPE_MAP[field.value as keyof typeof INTEGRATION_TYPE_MAP];
                            return meta?.icon ? (
                              <Icon icon={meta.icon} className={`size-4 ${meta.iconClass ?? ""}`} />
                            ) : null;
                          })()}
                          {INTEGRATION_TYPE_MAP[field.value as keyof typeof INTEGRATION_TYPE_MAP]?.label ?? field.value}
                        </span>
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {INTEGRATION_TYPES.map((t) => (
                        <SelectItem key={t.value} value={t.value} disabled={t.upcoming}>
                          <span className="inline-flex items-center gap-2">
                            <Icon icon={t.icon} className={`size-4 ${t.iconClass ?? ""} ${t.upcoming ? "opacity-40" : ""}`} />
                            <span className={t.upcoming ? "opacity-40" : ""}>{t.label}</span>
                            {t.upcoming && (
                              <span className="ml-auto rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground">
                                Soon
                              </span>
                            )}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
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
                  {...register("name", { required: "Name is required" })}
                  placeholder="e.g. Production"
                  className={inp(!!errors.name)}
                />
                {errors.name && (
                  <p className="text-xs text-destructive">{errors.name.message}</p>
                )}
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold">Description</label>
                <input
                  {...register("description")}
                  placeholder="Optional description"
                  className={inp(false)}
                />
              </div>

              {type === "GoogleCloud" && (
                <Controller
                  control={control}
                  name="serviceAccountJson"
                  rules={{ required: "Service account JSON is required" }}
                  render={({ field }) => (
                    <div className="flex flex-col gap-1">
                      <GoogleCloudConfig
                        serviceAccountJson={field.value}
                        setServiceAccountJson={field.onChange}
                      />
                      {errors.serviceAccountJson && (
                        <p className="text-xs text-destructive">{errors.serviceAccountJson.message}</p>
                      )}
                    </div>
                  )}
                />
              )}

              {type === "Jira" && (
                <JiraConfig
                  baseUrl={watch("jiraBaseUrl")}
                  email={watch("jiraEmail")}
                  apiToken={watch("jiraApiToken")}
                  projectKey={watch("jiraProjectKey")}
                  issueType={watch("jiraIssueType")}
                  errors={{
                    baseUrl:  errors.jiraBaseUrl?.message,
                    email:    errors.jiraEmail?.message,
                    apiToken: errors.jiraApiToken?.message,
                  }}
                  register={register}
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
                type="submit"
                disabled={isSubmitting}
                className="rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
              >
                {isSubmitting ? "Saving…" : isEdit ? "Save changes" : "Create Integration"}
              </button>
            </div>
          </div>
        </form>

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
