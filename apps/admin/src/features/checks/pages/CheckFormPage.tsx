import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { Settings } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { AdminLayout } from "@/components/AdminLayout";
import { useCreateCheck } from "@/hooks/useChecks";
import { useService } from "@/hooks/useServices";
import { checkTypesApi, integrationsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { SERVICE_STATUS, SERVICE_STATUS_LABEL, type ServiceStatus } from "@/constants/serviceStatus";
import { HttpConfig, DnsConfig, TcpConfig, PingConfig, SslConfig, HeartbeatConfig, GcpCloudRunJobConfig } from "@/features/checks/components";
import { CRON_PRESETS, CHECK_TYPE_LABELS, CHECK_TYPE_DEFAULTS } from "@/constants/checks";
import { slugify } from "@/utils/slugify";

// ── Types ────────────────────────────────────────────────────────────────────

type CheckType = string;

// ── Field helpers ─────────────────────────────────────────────────────────────

function Field({ label, required, hint, children }: {
  label: string; required?: boolean; hint?: string; children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-sm font-semibold">
        {label}{required && <span className="text-destructive ml-0.5">*</span>}
      </label>
      {children}
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

function buildConfig(type: CheckType, config: Record<string, unknown>): Record<string, unknown> {
  if (type === "HTTP") {
    const headers = (config.headers as { key: string; value: string }[]) ?? [];
    return {
      url: config.url,
      method: config.method ?? "GET",
      timeout: config.timeout ?? 5000,
      expectedStatusCodes: Array.isArray(config.expectedStatusCodes)
        ? config.expectedStatusCodes
        : String(config.expectedStatusCodes ?? "200").split(",").map((s) => parseInt(s.trim(), 10)).filter((n) => !isNaN(n)),
      followRedirects: config.followRedirects ?? true,
      body: config.body || undefined,
      headers: Object.fromEntries(headers.filter((h) => h.key).map((h) => [h.key, h.value])),
    };
  }
  if (type === "DNS") {
    const nameServers = ((config.nameServers as string[]) ?? []).filter(Boolean);
    return {
      host: config.host,
      recordType: config.recordType ?? "A",
      expectedValue: (config.expectedValue as string) || undefined,
      nameServers: nameServers.length > 0 ? nameServers : undefined,
      degradedAfter: config.degradedAfter || undefined,
      downAfter: config.downAfter || undefined,
    };
  }
  return config;
}


// ── Page ─────────────────────────────────────────────────────────────────────

export default function CheckFormPage() {
  const { slug: serviceSlug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const { data: service } = useService(serviceSlug!);
  const createCheck = useCreateCheck(serviceSlug!);

  const { data: checkTypes = [] } = useQuery({
    queryKey: QUERY_KEYS.CHECK_TYPES,
    queryFn: checkTypesApi.list,
  });
  const { data: integrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });

  const [name, setName] = useState("");
  const [checkSlug, setCheckSlug] = useState("");
  const [slugManual, setSlugManual] = useState(false);
  const [description, setDescription] = useState("");
  const [type, setType] = useState<CheckType>("HTTP");
  const [cronPreset, setCronPreset] = useState("* * * * *");
  const [customCron, setCustomCron] = useState("");
  const [showCustomCron, setShowCustomCron] = useState(false);
  const [isActive, setIsActive] = useState(true);
  const [defaultStatus, setDefaultStatus] = useState("NO_DATA");
  const [config, setConfig] = useState<Record<string, unknown>>(CHECK_TYPE_DEFAULTS.HTTP);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!slugManual) setCheckSlug(slugify(name));
  }, [name, slugManual]);

  function handleTypeChange(t: CheckType) {
    setType(t);
    setConfig(CHECK_TYPE_DEFAULTS[t] ?? {});
  }

  const effectiveCron = showCustomCron ? customCron : cronPreset;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      const integrationId = config.integrationId ? Number(config.integrationId) : undefined;
      const { integrationId: _removed, ...typeConfig } = config;
      void _removed;
      const check = await createCheck.mutateAsync({
        slug: checkSlug,
        name,
        type,
        cron: effectiveCron,
        typeDataJson: JSON.stringify(buildConfig(type, typeConfig)),
        defaultStatus,
        isActive,
        integrationId,
      });
      navigate(ROUTES.CHECKS.DETAIL(serviceSlug!, check.slug));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to create check.");
    }
  }

  return (
    <AdminLayout title="New Check">
      <form onSubmit={handleSubmit}>
        {/* Breadcrumb */}
        <nav className="flex items-center gap-2 text-sm text-muted-foreground mb-6">
          <button type="button" onClick={() => navigate(ROUTES.SERVICES.LIST)} className="hover:text-foreground transition-colors">
            Services
          </button>
          <span>/</span>
          <button type="button" onClick={() => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!))} className="hover:text-foreground transition-colors">
            {service?.name ?? serviceSlug}
          </button>
          <span>/</span>
          <span className="text-foreground font-medium">New Check</span>
        </nav>

        {error && (
          <div className="mb-4 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            {error}
          </div>
        )}

        {/* ── Card 1: General Settings ── */}
        <div className="rounded-xl border bg-card p-6 mb-4">
          <div className="flex items-center gap-2 mb-1">
            <Settings size={16} className="text-muted-foreground" />
            <h2 className="text-base font-semibold">General Settings</h2>
          </div>
          <p className="text-xs text-muted-foreground mb-5">Basic information about this check</p>

          <div className="flex flex-col gap-5">
            {/* Name + Slug */}
            <div className="grid grid-cols-2 gap-4">
              <Field label="Name" required>
                <Input value={name} onChange={(e) => setName(e.target.value)}
                  placeholder="Health Endpoint" />
              </Field>
              <Field label="Slug" required hint="Unique identifier within this service">
                <Input value={checkSlug}
                  onChange={(e) => { setSlugManual(true); setCheckSlug(e.target.value); }}
                  placeholder="health" className="font-mono" />
              </Field>
            </div>

            {/* Description */}
            <Field label="Description">
              <textarea value={description} rows={2}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="A brief description of what this check monitors"
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full disabled:cursor-not-allowed disabled:opacity-50 resize-none" />
            </Field>

            {/* Type + Cron */}
            <div className="grid grid-cols-2 gap-4">
              <Field label="Type" required>
                <Select value={type} onValueChange={(v) => v && handleTypeChange(v)}>
                  <SelectTrigger className="w-full">
                    <SelectValue>{(v: string) => CHECK_TYPE_LABELS[v] ?? v}</SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    {checkTypes.map((t) => (
                      <SelectItem key={t.type} value={t.type}>{CHECK_TYPE_LABELS[t.type] ?? t.type}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold">Cron Schedule</label>
                {showCustomCron ? (
                  <Input value={customCron} onChange={(e) => setCustomCron(e.target.value)}
                    placeholder="*/5 * * * *" className="font-mono" />
                ) : (
                  <Select value={cronPreset} onValueChange={(v) => v && setCronPreset(v)}>
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
                <p className="text-xs text-muted-foreground">How often to run this check</p>
              </div>
            </div>

            {/* Default Status + Active */}
            <div className="grid grid-cols-2 gap-4">
              <Field label="Default Status" hint="Status shown when no data is available">
                <Select value={defaultStatus} onValueChange={(v) => v && setDefaultStatus(v as ServiceStatus)}>
                  <SelectTrigger className="w-full">
                    <SelectValue>{(v: ServiceStatus) => SERVICE_STATUS_LABEL[v] ?? v}</SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    {Object.values(SERVICE_STATUS).map((s) => (
                      <SelectItem key={s} value={s}>{SERVICE_STATUS_LABEL[s]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <div className="flex flex-col gap-2">
                <label className="text-sm font-semibold">Active</label>
                <div className="flex items-center gap-2.5">
                  <Switch checked={isActive} onCheckedChange={setIsActive} />
                  <span className="text-sm text-muted-foreground">{isActive ? "Check is running" : "Check is paused"}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* ── Card 2: Type-specific Configuration ── */}
        <div className="rounded-xl border bg-card p-6 mb-6">
          <h2 className="text-base font-semibold mb-1">Configuration</h2>
          <p className="text-xs text-muted-foreground mb-5">Settings for the {type} check</p>

          {type === "HTTP" && <HttpConfig config={config} onChange={setConfig} />}
          {type === "DNS" && <DnsConfig config={config} onChange={setConfig} />}
          {type === "TCP" && <TcpConfig config={config} onChange={setConfig} />}
          {type === "Ping" && <PingConfig config={config} onChange={setConfig} />}
          {type === "SSL" && <SslConfig config={config} onChange={setConfig} />}
          {type === "Heartbeat" && <HeartbeatConfig config={config} onChange={setConfig} />}
          {type === "GCP_CloudRunJob" && (
            <GcpCloudRunJobConfig config={config} onChange={setConfig} integrations={integrations} />
          )}
        </div>

        {/* ── Footer actions ── */}
        <div className="flex items-center justify-between">
          <button type="button"
            onClick={() => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!))}
            className="rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted transition-colors">
            Cancel
          </button>
          <button type="submit" disabled={createCheck.isPending}
            className="flex items-center gap-2 rounded-lg bg-foreground text-background px-5 py-2 text-sm font-semibold hover:opacity-90 disabled:opacity-50 transition-opacity">
            <Settings size={14} />
            {createCheck.isPending ? "Creating…" : "Create Check"}
          </button>
        </div>
      </form>
    </AdminLayout>
  );
}
