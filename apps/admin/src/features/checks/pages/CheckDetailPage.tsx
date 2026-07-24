import { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { Bell, Play, Save, Settings, AlertTriangle, ClipboardList, Clock, Wrench } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import {
  useCheck,
  useUpdateCheck,
  useDeleteCheck,
  useRunCheck,
  useAlertConfigs,
} from "@/hooks/useChecks";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DynamicConfigForm from "@/components/config-form/DynamicConfigForm";
import { seedFromTypeData } from "@/components/config-form/seedDefaults";
import { validateConfig } from "@/components/config-form/validators";
import { CheckGeneralSettingsFields } from "@/features/checks/components/CheckGeneralSettingsFields";
import RequiredIntegrationPicker from "@/features/checks/components/RequiredIntegrationPicker";
import { AlertConfigsSection } from "@/features/checks/components/AlertConfigsSection";
import ScriptTestPanel from "@/features/checks/components/ScriptTestPanel";
import { checkConfigSchema, type CheckConfigFormValues } from "@/features/checks/validations";
import { CRON_PRESETS } from "@/constants/checks";
import { checkTypesApi } from "@/lib/actions/checks";
import StatusHistorySection from "../components/StatusHistorySection";
import RecentLogsSection from "../components/RecentLogsSection";
import DangerZone from "@/components/DangerZone";
import { StatusPill } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import RecentLogsActions from "../components/RecentLogsActions";

// ── General Settings ──────────────────────────────────────────────────────────

function GeneralSettingsSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data: check } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const { data: checkTypes = [] } = useQuery({
    queryKey: QUERY_KEYS.CHECK_TYPES,
    queryFn: checkTypesApi.list,
  });
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  // This section edits only the type-general fields; per-type config lives in ConfigurationSection,
  // so `config` stays an empty object here (never read on this form).
  const methods = useForm<CheckConfigFormValues>({
    resolver: zodResolver(checkConfigSchema),
    defaultValues: {
      name: "",
      slug: "",
      description: "",
      cron: "* * * * *",
      showCustomCron: false,
      isActive: true,
      isMultiRegion: false,
      type: "HTTP",
      config: {},
    },
  });

  useEffect(() => {
    if (!check) return;
    const isPreset = CRON_PRESETS.some((p) => p.value === check.cron);
    methods.reset({
      name: check.name,
      slug: check.slug,
      description: check.description ?? "",
      cron: check.cron ?? "* * * * *",
      showCustomCron: !isPreset,
      isActive: check.isActive,
      isMultiRegion: check.isMultiRegion,
      type: check.type,
      config: {},
    });
  }, [check, methods]);

  async function handleSave(values: CheckConfigFormValues) {
    setError("");
    try {
      await updateCheck.mutateAsync({
        name: values.name,
        description: values.description || undefined,
        cron: values.cron,
        isActive: values.isActive,
        isMultiRegion: values.isMultiRegion,
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save changes.");
    }
  }

  const typeLabel = check ? (checkTypes.find((t) => t.type === check.type)?.displayName ?? check.type) : "";
  const typeNode = (
    <input value={typeLabel} readOnly
      className="rounded-lg border bg-muted px-3 py-2 text-sm text-muted-foreground outline-none h-9 w-full" />
  );

  return (
    <FormProvider {...methods}>
      <form onSubmit={methods.handleSubmit(handleSave)} className="flex flex-col gap-5">
        {error && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            {error}
          </div>
        )}
        <CheckGeneralSettingsFields typeNode={typeNode} />
        <div className="flex justify-end">
          <button type="submit" disabled={updateCheck.isPending}
            className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity">
            <Save size={14} />
            {saved ? "Saved!" : updateCheck.isPending ? "Saving…" : "Save changes"}
          </button>
        </div>
      </form>
    </FormProvider>
  );
}

// ── Configuration ─────────────────────────────────────────────────────────────

function ConfigurationSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data: check } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const { data: checkTypes = [] } = useQuery({
    queryKey: QUERY_KEYS.CHECK_TYPES,
    queryFn: checkTypesApi.list,
  });
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");
  const [configValues, setConfigValues] = useState<Record<string, unknown>>({});
  const [configErrors, setConfigErrors] = useState<Record<string, string>>({});
  const [integrationError, setIntegrationError] = useState("");

  const typeMeta = check ? checkTypes.find((t) => t.type === check.type) : undefined;
  const requiredIntegration = typeMeta?.requiredIntegrationType;

  // The required integration lives inside the check's config (config.integrationInstanceId) — what the
  // check actually reads — so the picker reads/writes it there, and it's hidden from the schema form.
  const integrationInstanceId = (configValues.integrationInstanceId as string) ?? "";

  // Seed the config once both the check and its type manifest are available. integrationInstanceId
  // comes from the config itself (typeDataJson), not the legacy Check.integrationId field.
  const seeded = useRef(false);
  useEffect(() => {
    if (check && typeMeta && !seeded.current) {
      seeded.current = true;
      setConfigValues(seedFromTypeData(typeMeta.configSchema, check.typeDataJson));
    }
  }, [check, typeMeta]);

  async function handleSave() {
    setError("");
    // The integration-instance field is validated via the picker, not the generic schema form (hidden there).
    const schemaForValidation = (typeMeta?.configSchema ?? []).filter(
      (f) => !requiredIntegration || f.key !== "integrationInstanceId"
    );
    const errors = validateConfig(schemaForValidation, configValues);
    setConfigErrors(errors);
    const missingIntegration = !!requiredIntegration && !integrationInstanceId;
    setIntegrationError(missingIntegration ? `A ${requiredIntegration} integration is required.` : "");
    if (Object.keys(errors).length > 0 || missingIntegration) {
      setError("Fix the highlighted configuration fields before saving.");
      return;
    }
    try {
      await updateCheck.mutateAsync({ typeDataJson: JSON.stringify(configValues) });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save.");
    }
  }

  const schema = typeMeta?.configSchema ?? [];
  const visibleSchema = requiredIntegration ? schema.filter((f) => f.key !== "integrationInstanceId") : schema;

  return (
    <form onSubmit={(e) => { e.preventDefault(); handleSave(); }} className="flex flex-col gap-5">
      {error && (
        <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">{error}</div>
      )}

      {requiredIntegration && (
        <RequiredIntegrationPicker
          integrationType={requiredIntegration}
          value={integrationInstanceId}
          onChange={(id) => setConfigValues((prev) => ({ ...prev, integrationInstanceId: id }))}
          error={integrationError}
        />
      )}

      {schema.length === 0 && !requiredIntegration ? (
        <p className="text-sm text-muted-foreground">This check type has no configuration.</p>
      ) : visibleSchema.length > 0 ? (
        <DynamicConfigForm schema={visibleSchema} values={configValues} errors={configErrors} onChange={setConfigValues} />
      ) : null}

      {check?.type === "Script" && (
        <div className="border-t pt-4">
          <ScriptTestPanel
            serviceSlug={serviceSlug}
            checkSlug={checkSlug}
            getTypeDataJson={() => JSON.stringify(configValues)}
          />
        </div>
      )}

      <div className="flex justify-end">
        <Button type="submit" disabled={updateCheck.isPending}>
          <Save size={14} />
          {saved ? "Saved!" : updateCheck.isPending ? "Saving…" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function CheckDetailPage() {
  const { slug: serviceSlug, checkSlug } = useParams<{ slug: string; checkSlug: string }>();
  const navigate = useNavigate();
  const { data: check, isLoading } = useCheck(serviceSlug!, checkSlug!);
  const runCheck = useRunCheck(serviceSlug!, checkSlug!);
  const deleteCheck = useDeleteCheck(serviceSlug!, checkSlug!);
  const { data: checkTypes = [] } = useQuery({
    queryKey: QUERY_KEYS.CHECK_TYPES,
    queryFn: checkTypesApi.list,
  });
  const typeMeta = check ? checkTypes.find((t) => t.type === check.type) : undefined;
  const typeLabel = typeMeta?.displayName ?? check?.type ?? "";

  // For the section header warning: a check with no alert configs runs but notifies no one.
  const { data: alertConfigs } = useAlertConfigs(serviceSlug!, checkSlug!);
  const hasNoAlerts = alertConfigs !== undefined && alertConfigs.length === 0;

  async function handleDelete() {
    await deleteCheck.mutateAsync();
    navigate(ROUTES.SERVICES.DETAIL(serviceSlug!));
  }

  if (isLoading) {
    return (
      <>
        <div className="text-sm text-muted-foreground">Loading…</div>
      </>
    );
  }

  if (!check) {
    return (
      <>
        <div className="text-sm text-destructive">Check not found.</div>
      </>
    );
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Services", onClick: () => navigate(ROUTES.SERVICES.LIST) },
          { label: serviceSlug!, onClick: () => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!)) },
          { label: check.name },
        ]}
        actions={
          <>
            <span className="rounded-lg border px-3 py-1.5 text-sm text-muted-foreground">{typeLabel}</span>
            <StatusPill status={check.currentStatus}/>
            <Button
              onClick={() => runCheck.mutate()}
              disabled={runCheck.isPending}
              variant="outline"
            >
              <Play size={12} />
              {runCheck.isPending ? "Running…" : "Run now"}
            </Button>
          </>
        }
      />

      <SectionAccordion
        title="General Settings"
        description="Basic information about this check"
        icon={<Settings size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <GeneralSettingsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title="Configuration"
        description={`Settings for the ${typeLabel} check`}
        icon={<Wrench size={16} className="text-muted-foreground" />}
      >
        <ConfigurationSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title={
          hasNoAlerts ? (
            <span className="flex items-center gap-2">
              Alert Configurations
              <span className="flex items-center gap-1 rounded-full bg-amber-500/15 px-2 py-0.5 text-xs font-medium text-amber-600 dark:text-amber-400">
                <AlertTriangle size={12} />
                No alerts
              </span>
            </span>
          ) : (
            "Alert Configurations"
          )
        }
        description="Notification channels triggered by this check"
        icon={<Bell size={16} className="text-muted-foreground" />}
        disableCard
      >
        <AlertConfigsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} dimensions={typeMeta?.dimensions ?? []} />
      </SectionAccordion>

      <SectionAccordion
        title="Recent Logs"
        description="Latest check executions"
        icon={<ClipboardList size={16} className="text-muted-foreground" />}
        actions={<RecentLogsActions serviceSlug={serviceSlug!} checkSlug={checkSlug!} />}
        disableCard
      >
        <RecentLogsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} dimensions={typeMeta?.dimensions ?? []} />
      </SectionAccordion>

      <SectionAccordion
        title="Status History"
        description="Uptime and status over the last 14 days"
        icon={<Clock size={16} className="text-muted-foreground" />}
      >
        <StatusHistorySection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title="Danger Zone"
        description="Irreversible actions for this check"
        icon={<AlertTriangle size={16} className="text-destructive" />}
        titleClassName="text-destructive"
        disableCard
      >
        <DangerZone objectName="check" objectId={checkSlug!} onDelete={handleDelete} />
      </SectionAccordion>
    </>
  );
}
