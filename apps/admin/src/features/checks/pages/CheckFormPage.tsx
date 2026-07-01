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

// ── Types ────────────────────────────────────────────────────────────────────

type CheckType = string;

const CRON_PRESETS = [
  { label: "Every minute",     value: "* * * * *" },
  { label: "Every 5 minutes",  value: "*/5 * * * *" },
  { label: "Every 15 minutes", value: "*/15 * * * *" },
  { label: "Every 30 minutes", value: "*/30 * * * *" },
  { label: "Every hour",       value: "0 * * * *" },
  { label: "Every day",        value: "0 0 * * *" },
  { label: "Custom",           value: "custom" },
];

function slugify(str: string) {
  return str.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
}

// ── Toggle ───────────────────────────────────────────────────────────────────

function Toggle({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors ${
        checked ? "bg-foreground" : "bg-input"
      }`}
    >
      <span
        className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-background shadow-lg ring-0 transition-transform ${
          checked ? "translate-x-5" : "translate-x-0"
        }`}
      />
    </button>
  );
}

// ── Field helpers ─────────────────────────────────────────────────────────────

const inp = "rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";
const sel = "rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";

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

// ── Config panels per type ───────────────────────────────────────────────────

function HttpConfig({ config, onChange }: { config: Record<string, unknown>; onChange: (c: Record<string, unknown>) => void }) {
  const url = (config.url as string) ?? "";
  const method = (config.method as string) ?? "GET";
  const timeout = (config.timeout as number) ?? 5000;
  const codesRaw = config.expectedStatusCodes;
  const expectedStatusCodes = Array.isArray(codesRaw) ? codesRaw.join(", ") : ((codesRaw as string) ?? "200");
  const followRedirects = (config.followRedirects as boolean) ?? true;
  const body = (config.body as string) ?? "";
  const headers = (config.headers as { key: string; value: string }[]) ?? [{ key: "", value: "" }];

  return (
    <div className="flex flex-col gap-4">
      <Field label="URL" required>
        <input value={url} onChange={(e) => onChange({ ...config, url: e.target.value })}
          placeholder="https://example.com/health" className={inp} />
      </Field>

      <div className="grid grid-cols-2 gap-4">
        <Field label="Method">
          <select value={method} onChange={(e) => onChange({ ...config, method: e.target.value })} className={sel}>
            {["GET", "POST", "PUT", "PATCH", "DELETE"].map((m) => <option key={m}>{m}</option>)}
          </select>
        </Field>
        <Field label="Timeout (ms)">
          <input type="number" value={timeout}
            onChange={(e) => onChange({ ...config, timeout: Number(e.target.value) })} className={inp} />
        </Field>
      </div>

      <Field label="Expected Status Codes" hint="Comma-separated list of acceptable HTTP status codes">
        <input value={expectedStatusCodes}
          onChange={(e) => onChange({ ...config, expectedStatusCodes: e.target.value.split(",").map((s) => parseInt(s.trim(), 10)).filter((n) => !isNaN(n)) })}
          placeholder="200, 201" className={inp} />
      </Field>

      <label className="flex items-center gap-2 text-sm cursor-pointer">
        <input type="checkbox" checked={followRedirects}
          onChange={(e) => onChange({ ...config, followRedirects: e.target.checked })}
          className="size-4 rounded" />
        Follow Redirects
      </label>

      <Field label="Body">
        <textarea value={body} rows={3}
          onChange={(e) => onChange({ ...config, body: e.target.value })}
          className={`${inp} font-mono text-xs resize-none`} />
      </Field>

      <div>
        <label className="text-sm font-semibold block mb-2">Headers</label>
        {headers.map((h, i) => (
          <div key={i} className="flex gap-2 mb-2">
            <input placeholder="Key" value={h.key}
              onChange={(e) => { const hs = [...headers]; hs[i] = { ...hs[i], key: e.target.value }; onChange({ ...config, headers: hs }); }}
              className={inp} />
            <input placeholder="Value" value={h.value}
              onChange={(e) => { const hs = [...headers]; hs[i] = { ...hs[i], value: e.target.value }; onChange({ ...config, headers: hs }); }}
              className={inp} />
          </div>
        ))}
        <button type="button" onClick={() => onChange({ ...config, headers: [...headers, { key: "", value: "" }] })}
          className="text-sm hover:underline">
          + Add Header
        </button>
      </div>
    </div>
  );
}

function DnsConfig({ config, onChange }: { config: Record<string, unknown>; onChange: (c: Record<string, unknown>) => void }) {
  return (
    <div className="flex flex-col gap-4">
      <Field label="Host" required>
        <input value={(config.host as string) ?? ""} onChange={(e) => onChange({ ...config, host: e.target.value })}
          placeholder="example.com" className={inp} />
      </Field>
      <Field label="Record Type">
        <select value={(config.recordType as string) ?? "A"} onChange={(e) => onChange({ ...config, recordType: e.target.value })} className={sel}>
          {["A", "AAAA", "CNAME", "MX", "TXT", "NS"].map((t) => <option key={t}>{t}</option>)}
        </select>
      </Field>
      <Field label="Expected Values (one per line)">
        <textarea value={(config.expectedValues as string) ?? ""} rows={3}
          onChange={(e) => onChange({ ...config, expectedValues: e.target.value })}
          className={`${inp} font-mono text-xs resize-none`} />
      </Field>
      <Field label="Nameserver" hint="Optional. Leave blank to use system default.">
        <input value={(config.nameserver as string) ?? ""} onChange={(e) => onChange({ ...config, nameserver: e.target.value })}
          placeholder="8.8.8.8" className={inp} />
      </Field>
    </div>
  );
}

function TcpConfig({ config, onChange }: { config: Record<string, unknown>; onChange: (c: Record<string, unknown>) => void }) {
  return (
    <div className="grid grid-cols-2 gap-4">
      <Field label="Host" required>
        <input value={(config.host as string) ?? ""} onChange={(e) => onChange({ ...config, host: e.target.value })}
          placeholder="example.com" className={inp} />
      </Field>
      <Field label="Port" required>
        <input type="number" value={(config.port as number) ?? ""} onChange={(e) => onChange({ ...config, port: Number(e.target.value) })}
          placeholder="80" className={inp} />
      </Field>
    </div>
  );
}

function PingConfig({ config, onChange }: { config: Record<string, unknown>; onChange: (c: Record<string, unknown>) => void }) {
  return (
    <Field label="Host" required>
      <input value={(config.host as string) ?? ""} onChange={(e) => onChange({ ...config, host: e.target.value })}
        placeholder="example.com" className={inp} />
    </Field>
  );
}

function SslConfig({ config, onChange }: { config: Record<string, unknown>; onChange: (c: Record<string, unknown>) => void }) {
  return (
    <div className="flex flex-col gap-4">
      <Field label="Host" required>
        <input value={(config.host as string) ?? ""} onChange={(e) => onChange({ ...config, host: e.target.value })}
          placeholder="example.com" className={inp} />
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Port">
          <input type="number" value={(config.port as number) ?? 443} onChange={(e) => onChange({ ...config, port: Number(e.target.value) })} className={inp} />
        </Field>
        <Field label="Warn days before expiry">
          <input type="number" value={(config.warningDaysBeforeExpiry as number) ?? 30}
            onChange={(e) => onChange({ ...config, warningDaysBeforeExpiry: Number(e.target.value) })} className={inp} />
        </Field>
      </div>
    </div>
  );
}

function HeartbeatConfig({ config, onChange }: { config: Record<string, unknown>; onChange: (c: Record<string, unknown>) => void }) {
  return (
    <div className="flex flex-col gap-3">
      <Field label="Grace Period (seconds)">
        <input type="number" value={(config.gracePeriodSeconds as number) ?? 60}
          onChange={(e) => onChange({ ...config, gracePeriodSeconds: Number(e.target.value) })} className={inp} />
      </Field>
      <p className="text-sm text-muted-foreground">
        A heartbeat check waits for a ping from your service. If no ping is received within the grace period, the check is marked as down.
      </p>
    </div>
  );
}

function GcpCloudRunJobConfig({
  config, onChange, integrations,
}: {
  config: Record<string, unknown>;
  onChange: (c: Record<string, unknown>) => void;
  integrations: { id: number; name: string; type: string }[];
}) {
  const gcpIntegrations = integrations.filter((i) => i.type === "GoogleCloud");
  return (
    <div className="flex flex-col gap-4">
      <Field label="Google Cloud Integration" required>
        <select
          value={(config.integrationId as number | "") ?? ""}
          onChange={(e) => onChange({ ...config, integrationId: e.target.value ? Number(e.target.value) : "" })}
          className={sel}
        >
          <option value="">Select an integration…</option>
          {gcpIntegrations.map((i) => (
            <option key={i.id} value={i.id}>{i.name}</option>
          ))}
        </select>
        {gcpIntegrations.length === 0 && (
          <p className="text-xs text-amber-600 mt-1">
            No Google Cloud integrations found.{" "}
            <a href={ROUTES.INTEGRATIONS.NEW} className="underline">Create one first.</a>
          </p>
        )}
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Project ID" required>
          <input value={(config.projectId as string) ?? ""}
            onChange={(e) => onChange({ ...config, projectId: e.target.value })}
            placeholder="my-gcp-project" className={inp} />
        </Field>
        <Field label="Region" required>
          <input value={(config.region as string) ?? ""}
            onChange={(e) => onChange({ ...config, region: e.target.value })}
            placeholder="us-central1" className={inp} />
        </Field>
      </div>
      <Field label="Job Name" required>
        <input value={(config.jobName as string) ?? ""}
          onChange={(e) => onChange({ ...config, jobName: e.target.value })}
          placeholder="my-batch-job" className={inp} />
      </Field>
      <Field
        label="Max Age (hours)"
        hint="Mark as DOWN if no execution has completed within this window. Use 25 for a daily job."
      >
        <input type="number" value={(config.maxAgeHours as number) ?? 25}
          onChange={(e) => onChange({ ...config, maxAgeHours: Number(e.target.value) })}
          min={1} className={inp} />
      </Field>
    </div>
  );
}

function buildConfig(type: CheckType, config: Record<string, unknown>): Record<string, unknown> {
  if (type === "Http") {
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
  if (type === "Dns") {
    return {
      host: config.host,
      recordType: config.recordType ?? "A",
      expectedValues: ((config.expectedValues as string) ?? "").split("\n").map((v) => v.trim()).filter(Boolean),
      nameserver: config.nameserver || undefined,
    };
  }
  return config;
}

const TYPE_DEFAULTS: Record<string, Record<string, unknown>> = {
  Http:           { url: "", method: "GET", timeout: 5000, expectedStatusCodes: [200], followRedirects: true, body: "", headers: [{ key: "", value: "" }] },
  Dns:            { host: "", recordType: "A", expectedValues: "", nameserver: "" },
  Tcp:            { host: "", port: 80 },
  Ping:           { host: "" },
  Ssl:            { host: "", port: 443, warningDaysBeforeExpiry: 30 },
  Heartbeat:      { gracePeriodSeconds: 60 },
  GCP_CloudRunJob: { integrationId: "", projectId: "", region: "", jobName: "", maxAgeHours: 25 },
};

const TYPE_LABELS: Record<string, string> = {
  Http:           "HTTP",
  Dns:            "DNS",
  Tcp:            "TCP",
  Ping:           "Ping",
  Ssl:            "SSL",
  Heartbeat:      "Heartbeat",
  GCP_CloudRunJob: "GCP Cloud Run Job",
};

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
  const [type, setType] = useState<CheckType>("Http");
  const [cronPreset, setCronPreset] = useState("* * * * *");
  const [customCron, setCustomCron] = useState("");
  const [showCustomCron, setShowCustomCron] = useState(false);
  const [isActive, setIsActive] = useState(true);
  const [config, setConfig] = useState<Record<string, unknown>>(TYPE_DEFAULTS.Http);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!slugManual) setCheckSlug(slugify(name));
  }, [name, slugManual]);

  function handleTypeChange(t: CheckType) {
    setType(t);
    setConfig(TYPE_DEFAULTS[t] ?? {});
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
        defaultStatus: "NO_DATA",
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
                <input value={name} onChange={(e) => setName(e.target.value)}
                  placeholder="Health Endpoint" className={inp} />
              </Field>
              <Field label="Slug" required hint="Unique identifier within this service">
                <input value={checkSlug}
                  onChange={(e) => { setSlugManual(true); setCheckSlug(e.target.value); }}
                  placeholder="health" className={`${inp} font-mono`} />
              </Field>
            </div>

            {/* Description */}
            <Field label="Description">
              <textarea value={description} rows={2}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="A brief description of what this check monitors"
                className={`${inp} resize-none`} />
            </Field>

            {/* Type + Cron */}
            <div className="grid grid-cols-2 gap-4">
              <Field label="Type" required>
                <select value={type} onChange={(e) => handleTypeChange(e.target.value)} className={sel}>
                  {checkTypes.map((t) => (
                    <option key={t.type} value={t.type}>{TYPE_LABELS[t.type] ?? t.type}</option>
                  ))}
                </select>
              </Field>
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold">Cron Schedule</label>
                {showCustomCron ? (
                  <input value={customCron} onChange={(e) => setCustomCron(e.target.value)}
                    placeholder="*/5 * * * *" className={`${inp} font-mono`} />
                ) : (
                  <select value={cronPreset} onChange={(e) => setCronPreset(e.target.value)} className={sel}>
                    {CRON_PRESETS.filter((p) => p.value !== "custom").map((p) => (
                      <option key={p.value} value={p.value}>{p.label}</option>
                    ))}
                  </select>
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
                <select className={sel} defaultValue="unknown">
                  <option value="unknown">No data</option>
                  <option value="up">Up</option>
                  <option value="down">Down</option>
                </select>
              </Field>
              <div className="flex flex-col gap-2">
                <label className="text-sm font-semibold">Active</label>
                <div className="flex items-center gap-2.5">
                  <Toggle checked={isActive} onChange={setIsActive} />
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

          {type === "Http" && <HttpConfig config={config} onChange={setConfig} />}
          {type === "Dns" && <DnsConfig config={config} onChange={setConfig} />}
          {type === "Tcp" && <TcpConfig config={config} onChange={setConfig} />}
          {type === "Ping" && <PingConfig config={config} onChange={setConfig} />}
          {type === "Ssl" && <SslConfig config={config} onChange={setConfig} />}
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
