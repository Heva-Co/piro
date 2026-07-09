import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Bell, ExternalLink, Play, RefreshCw, Save, Settings, AlertTriangle, ClipboardList, Clock, Wrench } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import {
  useCheck,
  useUpdateCheck,
  useDeleteCheck,
  useRunCheck,
  useCheckLogs,
  useAlertConfigs,
  useCreateAlertConfig,
  useDeleteAlertConfig,
} from "@/hooks/useChecks";
import { channelsApi, integrationsApi } from "@/lib/api";
import { useForm, FormProvider } from "react-hook-form";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { StatusPill } from "@/components/StatusBadge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { HttpConfig, DnsConfig, TcpConfig, PingConfig, SslConfig, HeartbeatConfig, GcpCloudRunJobConfig } from "@/features/checks/components";
import { CheckGeneralSettingsFields, type CheckGeneralFormValues } from "@/features/checks/components/CheckGeneralSettingsFields";
import { CRON_PRESETS, CHECK_TYPE_LABELS } from "@/constants/checks";
import { formatTimestamp } from "@/utils/date";

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
      criticality: "High",
      autoCreate: false,
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
      criticality: (check.criticality as CheckGeneralFormValues["criticality"]) ?? "High",
      autoCreate: check.automaticallyCreateIncident ?? false,
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
        criticality: values.criticality,
        automaticallyCreateIncident: values.autoCreate,
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
  const [config, setConfig] = useState<Record<string, unknown>>({});
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!check) return;
    try {
      const parsed = check.typeDataJson ? JSON.parse(check.typeDataJson) : {};
      setConfig({ ...parsed, ...(check.integrationId != null ? { integrationId: check.integrationId } : {}) });
    } catch {
      setConfig({});
    }
  }, [check?.typeDataJson, check?.integrationId]);

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

// ── Recent Logs ───────────────────────────────────────────────────────────────

function RecentLogsSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data: logs, isLoading, isFetching, refetch } = useCheckLogs(serviceSlug, checkSlug);

  if (isLoading) return <div className="text-sm text-muted-foreground py-2">Loading…</div>;

  if (!logs || logs.length === 0) {
    return <div className="text-sm text-muted-foreground text-center py-6">No logs yet.</div>;
  }

  return (
    <div className="rounded-xl border bg-card overflow-hidden">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b bg-muted/40">
            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Time</th>
            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Latency</th>
            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Region</th>
            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Message</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {logs.map((log) => (
            <tr key={log.timestamp} className="hover:bg-muted/30 transition-colors">
              <td className="px-5 py-2.5 text-xs text-muted-foreground">{formatTimestamp(log.timestamp)}</td>
              <td className="px-5 py-2.5"><StatusPill status={log.status} dataType={log.dataType} /></td>
              <td className="px-5 py-2.5 text-sm text-muted-foreground">
                {log.latencyMs != null ? `${Math.round(log.latencyMs)} ms` : "—"}
              </td>
              <td className="px-5 py-2.5 text-xs text-muted-foreground">{log.workerRegion}</td>
              <td className="px-5 py-2.5 text-xs text-muted-foreground">{log.errorMessage ?? ""}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function RecentLogsActions({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const navigate = useNavigate();
  const { isFetching, refetch } = useCheckLogs(serviceSlug, checkSlug);
  return (
    <>
      <button onClick={() => refetch()} disabled={isFetching}
        className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors">
        <RefreshCw size={12} className={isFetching ? "animate-spin" : ""} />
        Refresh
      </button>
      <button onClick={() => navigate(ROUTES.CHECKS.LOGS(serviceSlug, checkSlug))}
        className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors">
        <ExternalLink size={12} />
        View all logs
      </button>
    </>
  );
}

// ── Alert Configurations ──────────────────────────────────────────────────────

function AlertConfigsSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data: alertConfigs, isLoading } = useAlertConfigs(serviceSlug, checkSlug);
  const { data: channels } = useQuery({ queryKey: QUERY_KEYS.CHANNELS, queryFn: channelsApi.list });
  const createAlertConfig = useCreateAlertConfig(serviceSlug, checkSlug);
  const deleteAlertConfig = useDeleteAlertConfig(serviceSlug, checkSlug);

  const [channelId, setChannelId] = useState<number | "">("");
  const [onDown, setOnDown] = useState(true);
  const [onRecovery, setOnRecovery] = useState(true);
  const [addError, setAddError] = useState("");

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    if (!channelId) return;
    setAddError("");
    try {
      await createAlertConfig.mutateAsync({ channelId: channelId as number, onDown, onRecovery });
      setChannelId("");
    } catch {
      setAddError("Failed to add alert configuration.");
    }
  }

  function channelName(id: number) {
    return channels?.find((c) => c.id === id)?.name ?? String(id);
  }

  return (
    <div className="rounded-xl border bg-card overflow-hidden">
      {isLoading ? (
        <div className="px-5 py-6 text-sm text-muted-foreground">Loading…</div>
      ) : !alertConfigs || alertConfigs.length === 0 ? (
        <div className="px-5 py-8 text-sm text-muted-foreground text-center">No alert configurations yet.</div>
      ) : (
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b bg-muted/40">
              <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Channel</th>
              <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">On Down</th>
              <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">On Recovery</th>
              <th className="px-5 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y">
            {alertConfigs.map((ac) => (
              <tr key={ac.id} className="hover:bg-muted/30 transition-colors">
                <td className="px-5 py-3 font-medium">{channelName(ac.channelId)}</td>
                <td className="px-5 py-3 text-muted-foreground">{ac.onDown ? "Yes" : "No"}</td>
                <td className="px-5 py-3 text-muted-foreground">{ac.onRecovery ? "Yes" : "No"}</td>
                <td className="px-5 py-3 text-right">
                  <button onClick={() => deleteAlertConfig.mutate(ac.id)}
                    className="text-sm text-destructive hover:opacity-70 font-medium">
                    Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="border-t px-5 py-4">
        <h4 className="text-sm font-semibold mb-3">Add Alert Configuration</h4>
        {addError && <p className="text-sm text-destructive mb-2">{addError}</p>}
        <form onSubmit={handleAdd} className="flex flex-wrap items-end gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-muted-foreground">Channel</label>
            <select required value={channelId} onChange={(e) => setChannelId(Number(e.target.value))}
              className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring">
              <option value="">Select channel</option>
              {channels?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </div>
          <label className="flex items-center gap-2 text-sm cursor-pointer pb-2">
            <input type="checkbox" checked={onDown} onChange={(e) => setOnDown(e.target.checked)} className="size-4 rounded" />
            On Down
          </label>
          <label className="flex items-center gap-2 text-sm cursor-pointer pb-2">
            <input type="checkbox" checked={onRecovery} onChange={(e) => setOnRecovery(e.target.checked)} className="size-4 rounded" />
            On Recovery
          </label>
          <button type="submit" disabled={createAlertConfig.isPending}
            className="rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity mb-0">
            {createAlertConfig.isPending ? "Adding…" : "Add"}
          </button>
        </form>
      </div>
    </div>
  );
}

// ── Danger Zone ───────────────────────────────────────────────────────────────

function DangerZone({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const navigate = useNavigate();
  const deleteCheck = useDeleteCheck(serviceSlug, checkSlug);
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState("");

  async function handleDelete() {
    if (confirm !== checkSlug) return;
    setError("");
    try {
      await deleteCheck.mutateAsync();
      navigate(ROUTES.SERVICES.DETAIL(serviceSlug));
    } catch {
      setError("Failed to delete check.");
    }
  }

  return (
    <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6 flex flex-col gap-4">
      <p className="text-sm">
        Permanently delete this check. Type{" "}
        <code className="font-mono font-semibold">{checkSlug}</code> to confirm.
      </p>
      {error && <p className="text-sm text-destructive">{error}</p>}
      <div className="flex items-center gap-3">
        <input value={confirm} onChange={(e) => setConfirm(e.target.value)} placeholder={checkSlug}
          className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-destructive w-64" />
        <button onClick={handleDelete} disabled={confirm !== checkSlug || deleteCheck.isPending}
          className="rounded-lg bg-destructive text-destructive-foreground px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-40 transition-opacity">
          {deleteCheck.isPending ? "Deleting…" : "Delete Check"}
        </button>
      </div>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function CheckDetailPage() {
  const { slug: serviceSlug, checkSlug } = useParams<{ slug: string; checkSlug: string }>();
  const navigate = useNavigate();
  const { data: check, isLoading } = useCheck(serviceSlug!, checkSlug!);
  const runCheck = useRunCheck(serviceSlug!, checkSlug!);

  if (isLoading) {
    return (
      <AdminLayout title="Check">
        <div className="text-sm text-muted-foreground">Loading…</div>
      </AdminLayout>
    );
  }

  if (!check) {
    return (
      <AdminLayout title="Check">
        <div className="text-sm text-destructive">Check not found.</div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout title={check.name}>
      {/* Breadcrumb + actions */}
      <div className="flex items-center justify-between mb-6">
        <nav className="flex items-center gap-2 text-sm text-muted-foreground">
          <button type="button" onClick={() => navigate(ROUTES.SERVICES.LIST)} className="hover:text-foreground transition-colors">
            Services
          </button>
          <span>/</span>
          <button type="button" onClick={() => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!))} className="hover:text-foreground transition-colors">
            {serviceSlug}
          </button>
          <span>/</span>
          <span className="text-foreground font-medium">{check.name}</span>
        </nav>
        <div className="flex items-center gap-2">
          <span className="rounded-lg border px-3 py-1.5 text-sm text-muted-foreground">{check.type}</span>
          <span className={`rounded-full px-3 py-1 text-xs font-semibold uppercase ${
            check.currentStatus === "UP" ? "bg-foreground text-background" : "border text-muted-foreground"
          }`}>
            {check.currentStatus === "NO_DATA" ? "No data" : check.currentStatus}
          </span>
          <button
            onClick={() => runCheck.mutate()}
            disabled={runCheck.isPending}
            className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors"
          >
            <Play size={12} />
            {runCheck.isPending ? "Running…" : "Run now"}
          </button>
        </div>
      </div>

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
        description="Uptime and status over time"
        icon={<Clock size={16} className="text-muted-foreground" />}
        upcomming
      />

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
        <DangerZone serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>
    </AdminLayout>
  );
}
