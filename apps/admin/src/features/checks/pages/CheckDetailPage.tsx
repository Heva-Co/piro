import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Bell, ExternalLink, Play, RefreshCw, Save, Settings, AlertTriangle, ClipboardList, Clock, Wrench } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import {
  useCheck,
  useUpdateCheck,
  useDeleteCheck,
  useRunCheck,
  useCheckLogs,
  useAlertConfigs,
  useCreateAlertConfig,
  useUpdateAlertConfig,
  useDeleteAlertConfig,
} from "@/hooks/useChecks";
import { integrationsApi } from "@/lib/api";
import { useForm, FormProvider } from "react-hook-form";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { HttpConfig, DnsConfig, TcpConfig, PingConfig, SslConfig, HeartbeatConfig, GcpCloudRunJobConfig } from "@/features/checks/components";
import { CheckGeneralSettingsFields, type CheckGeneralFormValues } from "@/features/checks/components/CheckGeneralSettingsFields";
import { CRON_PRESETS, CHECK_TYPE_LABELS } from "@/constants/checks";
import StatusHistorySection from "../components/StatusHistorySection";
import RecentLogsSection from "../components/RecentLogsSection";
import DangerZone from "@/components/DangerZone";
import { StatusPill } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import RecentLogsActions from "../components/RecentLogsActions";

// ── General Settings ──────────────────────────────────────────────────────────

function GeneralSettingsSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data: check } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  const methods = useForm<CheckGeneralFormValues>({
    defaultValues: {
      name: "",
      description: "",
      cron: "* * * * *",
      showCustomCron: false,
      isActive: true,
      isMultiRegion: false,
    },
  });

  useEffect(() => {
    if (!check) return;
    const isPreset = CRON_PRESETS.some((p) => p.value === check.cron);
    methods.reset({
      name: check.name,
      description: check.description ?? "",
      cron: check.cron ?? "* * * * *",
      showCustomCron: !isPreset,
      isActive: check.isActive,
      isMultiRegion: check.isMultiRegion,
    });
  }, [check, methods]);

  async function handleSave(values: CheckGeneralFormValues) {
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
    <input value={CHECK_TYPE_LABELS[check?.type ?? ""] ?? check?.type ?? ""} readOnly
      className="rounded-lg border bg-muted px-3 py-2 text-sm text-muted-foreground outline-none h-9 w-full" />
  );

  const slugNode = (
    <>
      <label className="text-sm font-semibold">Slug</label>
      <input value={checkSlug} readOnly
        className="rounded-lg border bg-muted px-3 py-2 text-sm text-muted-foreground outline-none h-9 w-full" />
      <p className="text-xs text-muted-foreground">Cannot be changed after creation</p>
    </>
  );

  return (
    <FormProvider {...methods}>
      <form onSubmit={methods.handleSubmit(handleSave)} className="rounded-xl border bg-card p-6 flex flex-col gap-5">
        {error && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            {error}
          </div>
        )}
        <CheckGeneralSettingsFields typeNode={typeNode} slugNode={slugNode} />
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
  const [config, setConfig] = useState<Record<string, unknown>>(() => {
    if (!check) return {};
    try {
      const parsed = check.typeDataJson ? JSON.parse(check.typeDataJson) : {};
      return { ...parsed, ...(check.integrationId != null ? { integrationId: check.integrationId } : {}) };
    } catch {
      return {};
    }
  });
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  async function handleSave() {
    setError("");
    try {
      const integrationId = config.integrationId ? Number(config.integrationId) : undefined;
      const { integrationId: _removed, ...typeConfig } = config;
      void _removed;
      await updateCheck.mutateAsync({
        typeDataJson: JSON.stringify(typeConfig),
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
    <div className="rounded-xl border bg-card p-6 flex flex-col gap-5">
      <p className="text-sm text-muted-foreground">Type-specific settings for the {CHECK_TYPE_LABELS[rawType] ?? rawType} check</p>
      {error && (
        <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">{error}</div>
      )}

      {rawType === "HTTP"      && <HttpConfig      config={config} onChange={setConfig} />}
      {rawType === "TCP"       && <TcpConfig       config={config} onChange={setConfig} />}
      {rawType === "DNS"       && <DnsConfig       config={config} onChange={setConfig} />}
      {rawType === "Ping"      && <PingConfig      config={config} onChange={setConfig} />}
      {rawType === "SSL"       && <SslConfig       config={config} onChange={setConfig} />}
      {rawType === "Heartbeat" && <HeartbeatConfig config={config} onChange={setConfig} />}
      {rawType === "GCP_CloudRunJob" && (
        <GcpCloudRunJobConfig
          config={config}
          onChange={setConfig}
          integrations={integrations}
        />
      )}

      <div className="flex justify-end">
        <button type="button" onClick={handleSave} disabled={updateCheck.isPending}
          className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity">
          <Save size={14} />
          {saved ? "Saved!" : updateCheck.isPending ? "Saving…" : "Save changes"}
        </button>
      </div>
    </div>
  );
}


const ALERT_FOR_OPTIONS = [
  { value: "Status", label: "Status" },
  { value: "Latency", label: "Latency" },
  { value: "Uptime", label: "Uptime" },
] as const;

// Restricted to one AlertConfig per Check for now — the backend enforces this with a unique
// index on CheckId. The form below edits that single config in place (creating it on first
// save) instead of listing/adding multiple rules.
function AlertConfigsSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data: alertConfigs, isLoading } = useAlertConfigs(serviceSlug, checkSlug);
  const createAlertConfig = useCreateAlertConfig(serviceSlug, checkSlug);
  const updateAlertConfig = useUpdateAlertConfig(serviceSlug, checkSlug);
  const deleteAlertConfig = useDeleteAlertConfig(serviceSlug, checkSlug);

  const existing = alertConfigs?.[0];

  const [alertFor, setAlertFor] = useState<"Status" | "Latency" | "Uptime">("Status");
  const [alertValue, setAlertValue] = useState("DOWN");
  const [failureThreshold, setFailureThreshold] = useState(1);
  const [successThreshold, setSuccessThreshold] = useState(1);
  const [severity, setSeverity] = useState<"Warning" | "Critical">("Critical");
  const [createIncident, setCreateIncident] = useState(false);
  const [incidentThresholdOccurrences, setIncidentThresholdOccurrences] = useState(1);
  const [isActive, setIsActive] = useState(true);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!existing) return;
    setAlertFor(existing.alertFor);
    setAlertValue(existing.alertValue);
    setFailureThreshold(existing.failureThreshold);
    setSuccessThreshold(existing.successThreshold);
    setSeverity(existing.severity);
    setCreateIncident(existing.createIncident);
    setIncidentThresholdOccurrences(existing.incidentThresholdOccurrences);
    setIsActive(existing.isActive);
  }, [existing]);

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    const data = {
      alertFor,
      alertValue,
      failureThreshold,
      successThreshold,
      severity,
      createIncident,
      incidentThresholdOccurrences,
      isActive,
    };
    try {
      if (existing) {
        await updateAlertConfig.mutateAsync({ id: existing.id, data });
      } else {
        await createAlertConfig.mutateAsync(data);
      }
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save alert configuration.");
    }
  }

  const isPending = createAlertConfig.isPending || updateAlertConfig.isPending;

  if (isLoading) {
    return (
      <div className="rounded-xl border bg-card overflow-hidden px-5 py-6 text-sm text-muted-foreground">
        Loading…
      </div>
    );
  }

  return (
    <div className="rounded-xl border bg-card overflow-hidden">
      <form onSubmit={handleSave} className="flex flex-col">
        {error && <p className="text-sm text-destructive px-5 pt-5">{error}</p>}

        {/* Trigger condition */}
        <div className="p-5 flex flex-col gap-4">
          <div>
            <p className="text-sm font-semibold">Trigger Condition</p>
            <p className="text-xs text-muted-foreground mt-0.5">What this check must do to be considered alerting</p>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-muted-foreground">Alert For</label>
              <Select value={alertFor} onValueChange={(v) => v && setAlertFor(v as typeof alertFor)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ALERT_FOR_OPTIONS.map((o) => <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-muted-foreground">Value</label>
              <Input required value={alertValue} onChange={(e) => setAlertValue(e.target.value)}
                placeholder={alertFor === "Status" ? "DOWN" : alertFor === "Latency" ? "5000" : "99.9"} />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-muted-foreground">Failure threshold</label>
              <Input type="number" min={1} value={failureThreshold}
                onChange={(e) => setFailureThreshold(Number(e.target.value))} />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-muted-foreground">Success threshold</label>
              <Input type="number" min={1} value={successThreshold}
                onChange={(e) => setSuccessThreshold(Number(e.target.value))} />
            </div>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-muted-foreground">Severity</label>
              <Select value={severity} onValueChange={(v) => v && setSeverity(v as typeof severity)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Warning">Warning</SelectItem>
                  <SelectItem value="Critical">Critical</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-2">
              <label className="text-xs font-medium text-muted-foreground">Active</label>
              <div className="flex items-center gap-2.5 h-9">
                <Switch checked={isActive} onCheckedChange={setIsActive} />
                <span className="text-sm text-muted-foreground">{isActive ? "Enabled" : "Disabled"}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Incident escalation */}
        <div className="border-t p-5 flex flex-col gap-4">
          <div>
            <p className="text-sm font-semibold">Incident Escalation</p>
            <p className="text-xs text-muted-foreground mt-0.5">Whether a firing alert should also create/attach an incident</p>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="flex flex-col gap-2">
              <label className="text-xs font-medium text-muted-foreground">Creates incident</label>
              <div className="flex items-center gap-2.5 h-9">
                <Switch checked={createIncident} onCheckedChange={setCreateIncident} />
                <span className="text-sm text-muted-foreground">{createIncident ? "Enabled" : "Disabled"}</span>
              </div>
            </div>
            {createIncident && (
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-muted-foreground">After N occurrences</label>
                <Input type="number" min={1} value={incidentThresholdOccurrences}
                  onChange={(e) => setIncidentThresholdOccurrences(Number(e.target.value))} />
              </div>
            )}
          </div>
        </div>

        {/* Actions */}
        <div className="border-t p-5 flex items-center gap-3">
          <Button type="submit" disabled={isPending}>
            {saved ? "Saved!" : isPending ? "Saving…" : "Save"}
          </Button>
          {existing && (
            <Button type="button" variant="ghost" onClick={() => deleteAlertConfig.mutate(existing.id)}
              className="text-destructive hover:text-destructive">
              Remove configuration
            </Button>
          )}
        </div>
      </form>
    </div>
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
            <span className="rounded-lg border px-3 py-1.5 text-sm text-muted-foreground">{check.type}</span>
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
        description={`Settings for the ${check.type} check`}
        icon={<Wrench size={16} className="text-muted-foreground" />}
      >
        <ConfigurationSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title="Recent Logs"
        description="Latest check executions"
        icon={<ClipboardList size={16} className="text-muted-foreground" />}
        actions={<RecentLogsActions serviceSlug={serviceSlug!} checkSlug={checkSlug!} />}
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
        title="Alert Configurations"
        description="Notification channels triggered by this check"
        icon={<Bell size={16} className="text-muted-foreground" />}
      >
        <AlertConfigsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title="Danger Zone"
        description="Irreversible actions for this check"
        icon={<AlertTriangle size={16} className="text-destructive" />}
        titleClassName="text-destructive"
      >
        <DangerZone objectName="check" objectId={checkSlug!} onDelete={handleDelete} />
      </SectionAccordion>
    </>
  );
}
