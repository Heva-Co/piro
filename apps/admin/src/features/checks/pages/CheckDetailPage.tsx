import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Bell, ExternalLink, Play, RefreshCw, Save } from "lucide-react";
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
import { CHECK_CRITICALITY_MAP, type CheckCriticalityKey } from "@/constants/checks";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { StatusPill } from "@/components/StatusBadge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { HttpConfig, DnsConfig, TcpConfig, PingConfig, SslConfig, HeartbeatConfig, GcpCloudRunJobConfig } from "@/features/checks/components";
import { CRON_PRESETS, CHECK_TYPE_LABELS } from "@/constants/checks";
import { formatTimestamp } from "@/utils/date";

// ── Helpers ───────────────────────────────────────────────────────────────────

type CheckType = string;



// ── General Settings ──────────────────────────────────────────────────────────

function GeneralSettingsSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data: check } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [type, setType] = useState<CheckType>("Http");
  const [cron, setCron] = useState("* * * * *");
  const [showCustomCron, setShowCustomCron] = useState(false);
  const [isActive, setIsActive] = useState(true);
  const [isMultiRegion, setIsMultiRegion] = useState(false);
  const [criticality, setCriticality] = useState<CheckCriticalityKey>("High");
  const [autoCreate, setAutoCreate] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!check) return;
    setName(check.name);
    setDescription(check.description ?? "");
    setType(check.type);
    setCron(check.cron ?? "* * * * *");
    setIsActive(check.isActive);
    setIsMultiRegion(check.isMultiRegion);
    setCriticality(check.criticality ?? "High");
    setAutoCreate(check.automaticallyCreateIncident ?? false);
    const isPreset = CRON_PRESETS.some((p) => p.value === check.cron);
    setShowCustomCron(!isPreset);
  }, [check]);

  async function handleSave() {
    setError("");
    try {
      await updateCheck.mutateAsync({
        name, description: description || undefined, type, cron, isActive, isMultiRegion,
        criticality, automaticallyCreateIncident: autoCreate,
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save changes.");
    }
  }

  return (
    <div className="rounded-xl border bg-card p-6 flex flex-col gap-5">
      {error && (
        <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      )}

      {/* Name + Slug */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Name <span className="text-destructive">*</span></label>
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Slug</label>
          <input value={checkSlug} readOnly className="rounded-lg border bg-muted px-3 py-2 text-sm text-muted-foreground outline-none" />
          <p className="text-xs text-muted-foreground">Cannot be changed after creation</p>
        </div>
      </div>

      {/* Description */}
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Description</label>
        <textarea value={description} rows={2} onChange={(e) => setDescription(e.target.value)}
          placeholder="A brief description" className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full resize-none" />
      </div>

      {/* Type + Cron */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Type</label>
          <input value={CHECK_TYPE_LABELS[type] ?? type} readOnly
            className="rounded-lg border bg-muted px-3 py-2 text-sm text-muted-foreground outline-none" />
          <p className="text-xs text-muted-foreground">Cannot be changed after creation</p>
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Cron Schedule</label>
          {showCustomCron ? (
            <Input value={cron} onChange={(e) => setCron(e.target.value)} placeholder="*/5 * * * *" className="font-mono" />
          ) : (
            <Select value={cron} onValueChange={(v) => v && setCron(v)}>
              <SelectTrigger className="w-full">
                <SelectValue>{(v: string) => CRON_PRESETS.find((p) => p.value === v)?.label ?? v}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {CRON_PRESETS.filter((p) => p.value !== "custom").map((p) => (
                  <SelectItem key={p.value} value={p.value}>{p.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
          <button type="button" onClick={() => setShowCustomCron((v) => !v)}
            className="text-xs text-left hover:underline w-fit">
            {showCustomCron ? "← Use preset" : "Enter custom cron →"}
          </button>
        </div>
      </div>

      {/* Active + Multi-region */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-2">
          <label className="text-sm font-semibold">Active</label>
          <div className="flex items-center gap-2.5">
            <Switch checked={isActive} onCheckedChange={setIsActive} />
            <span className="text-sm text-muted-foreground">{isActive ? "Running" : "Paused"}</span>
          </div>
        </div>
        <div className="flex flex-col gap-2">
          <label className="text-sm font-semibold">Multi-region</label>
          <div className="flex items-center gap-2.5">
            <Switch checked={isMultiRegion} onCheckedChange={setIsMultiRegion} />
            <span className="text-sm text-muted-foreground">{isMultiRegion ? "Enabled" : "Disabled"}</span>
          </div>
        </div>
      </div>

      {/* Incident automation */}
      <div className="border-t pt-5 flex flex-col gap-4">
        <div>
          <p className="text-sm font-semibold">Incident Automation</p>
          <p className="text-xs text-muted-foreground mt-0.5">Configure how this check interacts with incident management</p>
        </div>

        <div className="grid grid-cols-3 gap-4">
          {/* Criticality */}
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-semibold">Criticality</label>
            <Select value={criticality} onValueChange={(v) => v && setCriticality(v)}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {(Object.entries(CHECK_CRITICALITY_MAP) as [CheckCriticalityKey, { label: string; description: string }][]).map(([key, meta]) => (
                  <SelectItem key={key} value={key}>
                    {meta.label} — {meta.description}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">Determines incident impact when auto-created</p>
          </div>

          {/* Auto-create */}
          <div className="flex flex-col gap-2">
            <label className="text-sm font-semibold">Auto-create incident</label>
            <div className="flex items-center gap-2.5">
              <Switch checked={autoCreate} onCheckedChange={setAutoCreate} />
              <span className="text-sm text-muted-foreground">{autoCreate ? "Enabled" : "Disabled"}</span>
            </div>
            <p className="text-xs text-muted-foreground">Creates an internal incident when this check starts alerting</p>
          </div>

        </div>
      </div>

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
  const navigate = useNavigate();
  const { data: logs, isLoading, isFetching, refetch } = useCheckLogs(serviceSlug, checkSlug);

  return (
    <div className="rounded-xl border bg-card overflow-hidden">
      <div className="flex items-center justify-between px-5 py-3 border-b">
        <p className="text-sm text-muted-foreground">Recent check results</p>
        <div className="flex items-center gap-2">
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
        </div>
      </div>
      {isLoading ? (
        <div className="px-5 py-6 text-sm text-muted-foreground">Loading…</div>
      ) : !logs || logs.length === 0 ? (
        <div className="px-5 py-8 text-sm text-muted-foreground text-center">No logs yet.</div>
      ) : (
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
                <td className="px-5 py-2.5 text-xs text-muted-foreground">
                  {formatTimestamp(log.timestamp)}
                </td>
                <td className="px-5 py-2.5">
                  <StatusPill status={log.status} dataType={log.dataType} />
                </td>
                <td className="px-5 py-2.5 text-sm text-muted-foreground">
                  {log.latencyMs != null ? `${Math.round(log.latencyMs)} ms` : "—"}
                </td>
                <td className="px-5 py-2.5 text-xs text-muted-foreground">{log.workerRegion}</td>
                <td className="px-5 py-2.5 text-xs text-muted-foreground">{log.errorMessage ?? ""}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
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

      <SectionAccordion title="General Settings" defaultOpen>
        <GeneralSettingsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion title="Configuration">
        <ConfigurationSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion title="Recent Logs">
        <RecentLogsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion title="Status History" upcomming />

      <SectionAccordion title={
        <span className="flex items-center gap-1.5"><Bell size={14} />Alert Configurations</span>
      }>
        <AlertConfigsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion title="Danger Zone" titleClassName="text-destructive">
        <DangerZone serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>
    </AdminLayout>
  );
}
