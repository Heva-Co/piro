import { useState, useEffect } from "react";
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
} from "@/hooks/useChecks";
import { integrationsApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { CheckTypeConfigFields } from "@/features/checks/components/CheckTypeConfigFields";
import { CheckGeneralSettingsFields } from "@/features/checks/components/CheckGeneralSettingsFields";
import { AlertConfigsSection } from "@/features/checks/components/AlertConfigsSection";
import { checkConfigSchema, CHECK_CONFIG_DEFAULTS, type CheckConfigFormValues } from "@/features/checks/validations";
import { CRON_PRESETS, CHECK_TYPE_LABELS } from "@/constants/checks";
import { buildTypeDataJson } from "@/features/checks/utils/typeDataJson";
import StatusHistorySection from "../components/StatusHistorySection";
import RecentLogsSection from "../components/RecentLogsSection";
import DangerZone from "@/components/DangerZone";
import { StatusPill } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import RecentLogsActions from "../components/RecentLogsActions";
import type { Check } from "@/lib/actions/checks";

/** Maps a persisted check's typeDataJson back into the flat CheckConfigFormValues shape. */
function parseTypeDataJson(check: Check): Partial<CheckConfigFormValues> {
  let parsed: Record<string, unknown> = {};
  try {
    parsed = check.typeDataJson ? JSON.parse(check.typeDataJson) : {};
  } catch {
    parsed = {};
  }

  switch (check.type) {
    case "HTTP": {
      const rawHeaders = parsed.headers;
      const headers = rawHeaders && typeof rawHeaders === "object"
        ? Object.entries(rawHeaders as Record<string, string>).map(([key, value]) => ({ key, value }))
        : [];
      const rawCodes = parsed.expectedStatusCodes;
      const expectedStatusCodes = Array.isArray(rawCodes) ? rawCodes.join(", ") : String(rawCodes ?? "200");
      return {
        url: (parsed.url as string) ?? "",
        method: (parsed.method as string) ?? "GET",
        timeout: (parsed.timeout as number) ?? 5000,
        expectedStatusCodes,
        followRedirects: (parsed.followRedirects as boolean) ?? true,
        body: (parsed.body as string) ?? "",
        headers,
        responseRules: (parsed.responseRules as CheckConfigFormValues["responseRules"]) ?? [],
      };
    }
    case "DNS":
      return {
        host: (parsed.host as string) ?? "",
        recordType: (parsed.recordType as string) ?? "A",
        expectedValue: (parsed.expectedValue as string) ?? "",
        nameServers: (parsed.nameServers as string[]) ?? [],
      };
    case "TCP":
      return { host: (parsed.host as string) ?? "", port: (parsed.port as number) ?? 80 };
    case "Ping":
      return { host: (parsed.host as string) ?? "" };
    case "SSL":
      return { host: (parsed.host as string) ?? "", port: (parsed.port as number) ?? 443 };
    case "Heartbeat":
      return { gracePeriodSeconds: (parsed.gracePeriodSeconds as number) ?? 60 };
    case "GCP_CloudRunJob":
      return {
        projectId: (parsed.projectId as string) ?? "",
        region: (parsed.region as string) ?? "",
        jobName: (parsed.jobName as string) ?? "",
        maxAgeHours: (parsed.maxAgeHours as number) ?? 25,
        integrationId: check.integrationId ?? "",
      };
    default:
      return {};
  }
}

// ── General Settings ──────────────────────────────────────────────────────────

function GeneralSettingsSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data: check } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

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
      ...CHECK_CONFIG_DEFAULTS,
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
      ...CHECK_CONFIG_DEFAULTS,
      ...parseTypeDataJson(check),
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

  const typeNode = (
    <input value={check ? (CHECK_TYPE_LABELS[check.type] ?? check.type) : ""} readOnly
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
  const { data: integrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

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
      ...CHECK_CONFIG_DEFAULTS,
    },
  });

  useEffect(() => {
    if (!check) return;
    methods.reset({
      name: check.name,
      slug: check.slug,
      description: check.description ?? "",
      cron: check.cron,
      showCustomCron: false,
      isActive: check.isActive,
      isMultiRegion: check.isMultiRegion,
      type: check.type,
      ...CHECK_CONFIG_DEFAULTS,
      ...parseTypeDataJson(check),
    });
  }, [check, methods]);

  async function handleSave(values: CheckConfigFormValues) {
    setError("");
    try {
      const integrationId = values.integrationId ? values.integrationId : undefined;
      await updateCheck.mutateAsync({
        typeDataJson: buildTypeDataJson(values),
        ...(integrationId != null ? { integrationId } : {}),
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save.");
    }
  }

  const rawType = check?.type ?? "HTTP";

  return (
    <FormProvider {...methods}>
      <form onSubmit={methods.handleSubmit(handleSave)} className="flex flex-col gap-5">
        {error && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">{error}</div>
        )}

        <CheckTypeConfigFields type={rawType} integrations={integrations} />

        <div className="flex justify-end">
          <Button type="submit" disabled={updateCheck.isPending}>
            <Save size={14} />
            {saved ? "Saved!" : updateCheck.isPending ? "Saving…" : "Save changes"}
          </Button>
        </div>
      </form>
    </FormProvider>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function CheckDetailPage() {
  const { slug: serviceSlug, checkSlug } = useParams<{ slug: string; checkSlug: string }>();
  const navigate = useNavigate();
  const { data: check, isLoading } = useCheck(serviceSlug!, checkSlug!);
  const runCheck = useRunCheck(serviceSlug!, checkSlug!);
  const deleteCheck = useDeleteCheck(serviceSlug!, checkSlug!);

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
            <span className="rounded-lg border px-3 py-1.5 text-sm text-muted-foreground">{CHECK_TYPE_LABELS[check.type] ?? check.type}</span>
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
        description={`Settings for the ${CHECK_TYPE_LABELS[check.type] ?? check.type} check`}
        icon={<Wrench size={16} className="text-muted-foreground" />}
      >
        <ConfigurationSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title="Alert Configurations"
        description="Notification channels triggered by this check"
        icon={<Bell size={16} className="text-muted-foreground" />}
        disableCard
      >
        <AlertConfigsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} checkType={check.type} />
      </SectionAccordion>

      <SectionAccordion
        title="Recent Logs"
        description="Latest check executions"
        icon={<ClipboardList size={16} className="text-muted-foreground" />}
        actions={<RecentLogsActions serviceSlug={serviceSlug!} checkSlug={checkSlug!} />}
        disableCard
      >
        <RecentLogsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
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
