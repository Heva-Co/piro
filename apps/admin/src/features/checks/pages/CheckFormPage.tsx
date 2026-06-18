import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { AdminLayout } from "@/components/AdminLayout";
import { useCreateCheck } from "@/hooks/useChecks";
import { ROUTES } from "@/constants/routes";

type CheckType = "Http" | "Dns" | "Tcp" | "Ping" | "Ssl" | "Heartbeat";
const CHECK_TYPES: CheckType[] = ["Http", "Dns", "Tcp", "Ping", "Ssl", "Heartbeat"];

function slugify(str: string): string {
  return str
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

interface HttpConfig {
  url: string;
  method: string;
  timeout: number;
  expectedStatusCodes: string;
  followRedirects: boolean;
  body: string;
  headers: { key: string; value: string }[];
}

interface DnsConfig {
  host: string;
  recordType: string;
  expectedValues: string;
  nameserver: string;
}

interface TcpConfig {
  host: string;
  port: string;
}

interface PingConfig {
  host: string;
}

interface SslConfig {
  host: string;
  port: number;
  warningDaysBeforeExpiry: number;
}

interface HeartbeatConfig {
  gracePeriodSeconds: number;
}

function buildTypeDataJson(
  type: CheckType,
  http: HttpConfig,
  dns: DnsConfig,
  tcp: TcpConfig,
  ping: PingConfig,
  ssl: SslConfig,
  heartbeat: HeartbeatConfig
): string {
  switch (type) {
    case "Http":
      return JSON.stringify({
        url: http.url,
        method: http.method,
        timeout: http.timeout,
        expectedStatusCodes: http.expectedStatusCodes,
        followRedirects: http.followRedirects,
        body: http.body || undefined,
        headers: Object.fromEntries(
          http.headers.filter((h) => h.key).map((h) => [h.key, h.value])
        ),
      });
    case "Dns":
      return JSON.stringify({
        host: dns.host,
        recordType: dns.recordType,
        expectedValues: dns.expectedValues
          .split("\n")
          .map((v) => v.trim())
          .filter(Boolean),
        nameserver: dns.nameserver || undefined,
      });
    case "Tcp":
      return JSON.stringify({ host: tcp.host, port: Number(tcp.port) });
    case "Ping":
      return JSON.stringify({ host: ping.host });
    case "Ssl":
      return JSON.stringify({
        host: ssl.host,
        port: ssl.port,
        warningDaysBeforeExpiry: ssl.warningDaysBeforeExpiry,
      });
    case "Heartbeat":
      return JSON.stringify({ gracePeriodSeconds: heartbeat.gracePeriodSeconds });
  }
}

export default function CheckFormPage() {
  const { slug: serviceSlug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const createCheck = useCreateCheck(serviceSlug!);
  const [error, setError] = useState<string | null>(null);

  // General fields
  const [name, setName] = useState("");
  const [checkSlug, setCheckSlug] = useState("");
  const [slugManual, setSlugManual] = useState(false);
  const [description, setDescription] = useState("");
  const [type, setType] = useState<CheckType>("Http");
  const [cron, setCron] = useState("*/5 * * * *");
  const [isActive, setIsActive] = useState(true);

  // Http config
  const [http, setHttp] = useState<HttpConfig>({
    url: "",
    method: "GET",
    timeout: 30,
    expectedStatusCodes: "200-299",
    followRedirects: true,
    body: "",
    headers: [{ key: "", value: "" }],
  });

  // Dns config
  const [dns, setDns] = useState<DnsConfig>({
    host: "",
    recordType: "A",
    expectedValues: "",
    nameserver: "",
  });

  // Tcp config
  const [tcp, setTcp] = useState<TcpConfig>({ host: "", port: "" });

  // Ping config
  const [ping, setPing] = useState<PingConfig>({ host: "" });

  // Ssl config
  const [ssl, setSsl] = useState<SslConfig>({ host: "", port: 443, warningDaysBeforeExpiry: 30 });

  // Heartbeat config
  const [heartbeat, setHeartbeat] = useState<HeartbeatConfig>({ gracePeriodSeconds: 60 });

  useEffect(() => {
    if (!slugManual) {
      setCheckSlug(slugify(name));
    }
  }, [name, slugManual]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const typeDataJson = buildTypeDataJson(type, http, dns, tcp, ping, ssl, heartbeat);
    try {
      const check = await createCheck.mutateAsync({
        name,
        type,
        isActive,
        interval: 0, // derived from cron server-side
        config: { cron, typeDataJson },
      });
      navigate(ROUTES.CHECKS.DETAIL(serviceSlug!, check.slug));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to create check.");
    }
  }

  function inputClass(extraClass = "") {
    return `border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 ${extraClass}`;
  }

  return (
    <AdminLayout title="New Check">
      <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-6 max-w-2xl">
        <form onSubmit={handleSubmit} className="space-y-5">
          {error && (
            <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md p-3">
              {error}
            </div>
          )}

          {/* Name */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              className={inputClass("w-full")}
            />
          </div>

          {/* Slug */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Slug <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              required
              value={checkSlug}
              onChange={(e) => {
                setSlugManual(true);
                setCheckSlug(e.target.value);
              }}
              className={inputClass("w-full font-mono")}
            />
          </div>

          {/* Description */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={2}
              className={inputClass("w-full")}
            />
          </div>

          {/* Type */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Type</label>
            <select
              value={type}
              onChange={(e) => setType(e.target.value as CheckType)}
              className={inputClass("w-48")}
            >
              {CHECK_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </div>

          {/* Cron */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Cron Schedule</label>
            <input
              type="text"
              value={cron}
              onChange={(e) => setCron(e.target.value)}
              className={inputClass("w-56 font-mono")}
              placeholder="*/5 * * * *"
            />
            <p className="text-xs text-gray-400 mt-1">Standard cron expression (e.g. "*/5 * * * *" = every 5 minutes)</p>
          </div>

          {/* isActive */}
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
              className="rounded border-gray-300 text-indigo-600"
            />
            Active
          </label>

          {/* Type-specific config */}
          <div className="border-t border-gray-100 pt-5">
            <h3 className="text-sm font-semibold text-gray-700 mb-4">{type} Configuration</h3>

            {type === "Http" && (
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    URL <span className="text-red-500">*</span>
                  </label>
                  <input
                    required
                    type="url"
                    value={http.url}
                    onChange={(e) => setHttp({ ...http, url: e.target.value })}
                    className={inputClass("w-full")}
                    placeholder="https://example.com"
                  />
                </div>
                <div className="flex gap-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Method</label>
                    <select
                      value={http.method}
                      onChange={(e) => setHttp({ ...http, method: e.target.value })}
                      className={inputClass()}
                    >
                      {["GET", "POST", "PUT", "PATCH", "DELETE"].map((m) => (
                        <option key={m}>{m}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Timeout (s)
                    </label>
                    <input
                      type="number"
                      value={http.timeout}
                      onChange={(e) => setHttp({ ...http, timeout: Number(e.target.value) })}
                      className={inputClass("w-24")}
                    />
                  </div>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Expected Status Codes
                  </label>
                  <input
                    type="text"
                    value={http.expectedStatusCodes}
                    onChange={(e) => setHttp({ ...http, expectedStatusCodes: e.target.value })}
                    className={inputClass("w-48")}
                    placeholder="200-299"
                  />
                </div>
                <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={http.followRedirects}
                    onChange={(e) => setHttp({ ...http, followRedirects: e.target.checked })}
                    className="rounded border-gray-300 text-indigo-600"
                  />
                  Follow Redirects
                </label>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Body</label>
                  <textarea
                    value={http.body}
                    onChange={(e) => setHttp({ ...http, body: e.target.value })}
                    rows={3}
                    className={inputClass("w-full font-mono text-xs")}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">Headers</label>
                  {http.headers.map((h, i) => (
                    <div key={i} className="flex gap-2 mb-2">
                      <input
                        placeholder="Key"
                        value={h.key}
                        onChange={(e) => {
                          const headers = [...http.headers];
                          headers[i] = { ...headers[i], key: e.target.value };
                          setHttp({ ...http, headers });
                        }}
                        className={inputClass("flex-1")}
                      />
                      <input
                        placeholder="Value"
                        value={h.value}
                        onChange={(e) => {
                          const headers = [...http.headers];
                          headers[i] = { ...headers[i], value: e.target.value };
                          setHttp({ ...http, headers });
                        }}
                        className={inputClass("flex-1")}
                      />
                    </div>
                  ))}
                  <button
                    type="button"
                    onClick={() => setHttp({ ...http, headers: [...http.headers, { key: "", value: "" }] })}
                    className="text-sm text-indigo-600 hover:text-indigo-800"
                  >
                    + Add Header
                  </button>
                </div>
              </div>
            )}

            {type === "Dns" && (
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Host <span className="text-red-500">*</span>
                  </label>
                  <input
                    required
                    type="text"
                    value={dns.host}
                    onChange={(e) => setDns({ ...dns, host: e.target.value })}
                    className={inputClass("w-full")}
                    placeholder="example.com"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Record Type</label>
                  <select
                    value={dns.recordType}
                    onChange={(e) => setDns({ ...dns, recordType: e.target.value })}
                    className={inputClass()}
                  >
                    {["A", "AAAA", "CNAME", "MX", "TXT", "NS"].map((t) => (
                      <option key={t}>{t}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Expected Values (one per line)
                  </label>
                  <textarea
                    value={dns.expectedValues}
                    onChange={(e) => setDns({ ...dns, expectedValues: e.target.value })}
                    rows={3}
                    className={inputClass("w-full font-mono text-xs")}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Nameserver (optional)</label>
                  <input
                    type="text"
                    value={dns.nameserver}
                    onChange={(e) => setDns({ ...dns, nameserver: e.target.value })}
                    className={inputClass("w-full")}
                    placeholder="8.8.8.8"
                  />
                </div>
              </div>
            )}

            {type === "Tcp" && (
              <div className="flex gap-4">
                <div className="flex-1">
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Host <span className="text-red-500">*</span>
                  </label>
                  <input
                    required
                    type="text"
                    value={tcp.host}
                    onChange={(e) => setTcp({ ...tcp, host: e.target.value })}
                    className={inputClass("w-full")}
                    placeholder="example.com"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Port <span className="text-red-500">*</span>
                  </label>
                  <input
                    required
                    type="number"
                    value={tcp.port}
                    onChange={(e) => setTcp({ ...tcp, port: e.target.value })}
                    className={inputClass("w-24")}
                    placeholder="80"
                  />
                </div>
              </div>
            )}

            {type === "Ping" && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Host <span className="text-red-500">*</span>
                </label>
                <input
                  required
                  type="text"
                  value={ping.host}
                  onChange={(e) => setPing({ host: e.target.value })}
                  className={inputClass("w-full")}
                  placeholder="example.com"
                />
              </div>
            )}

            {type === "Ssl" && (
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Host <span className="text-red-500">*</span>
                  </label>
                  <input
                    required
                    type="text"
                    value={ssl.host}
                    onChange={(e) => setSsl({ ...ssl, host: e.target.value })}
                    className={inputClass("w-full")}
                    placeholder="example.com"
                  />
                </div>
                <div className="flex gap-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Port</label>
                    <input
                      type="number"
                      value={ssl.port}
                      onChange={(e) => setSsl({ ...ssl, port: Number(e.target.value) })}
                      className={inputClass("w-24")}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Warn days before expiry
                    </label>
                    <input
                      type="number"
                      value={ssl.warningDaysBeforeExpiry}
                      onChange={(e) =>
                        setSsl({ ...ssl, warningDaysBeforeExpiry: Number(e.target.value) })
                      }
                      className={inputClass("w-24")}
                    />
                  </div>
                </div>
              </div>
            )}

            {type === "Heartbeat" && (
              <div className="space-y-3">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Grace Period (seconds)
                  </label>
                  <input
                    type="number"
                    value={heartbeat.gracePeriodSeconds}
                    onChange={(e) =>
                      setHeartbeat({ gracePeriodSeconds: Number(e.target.value) })
                    }
                    className={inputClass("w-32")}
                  />
                </div>
                <p className="text-sm text-gray-500">
                  A heartbeat check waits for a ping from your service. If no ping is received
                  within the grace period, the check is marked as down. The heartbeat URL will be
                  shown on the check detail page.
                </p>
              </div>
            )}
          </div>

          <div className="pt-2">
            <button
              type="submit"
              disabled={createCheck.isPending}
              className="px-4 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
            >
              {createCheck.isPending ? "Creating..." : "Create Check"}
            </button>
          </div>
        </form>
      </div>
    </AdminLayout>
  );
}
