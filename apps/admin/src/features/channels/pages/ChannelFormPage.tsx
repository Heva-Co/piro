import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, FlaskConical } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { channelsApi } from "@/lib/api";
import { CHANNEL_TYPE_LABELS, QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

// ── Types ─────────────────────────────────────────────────────────────────────

const CHANNEL_TYPES = [
  "Webhook", "Email", "Slack", "PagerDuty", "MSTeams", "Telegram",
  "TwilioSms", "GoogleChat", "Discord", "Opsgenie", "Pushover", "Ntfy",
];

interface Header { key: string; value: string }

// ── Styles ────────────────────────────────────────────────────────────────────

const inp = "rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";
const sel = "rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";

// ── Toggle row ────────────────────────────────────────────────────────────────

function ToggleRow({
  label, description, checked, onChange,
}: { label: string; description: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <div className="flex items-center justify-between border-b border-border py-4">
      <div>
        <p className="text-sm font-semibold">{label}</p>
        <p className="text-xs text-muted-foreground mt-0.5">{description}</p>
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors ${
          checked ? "bg-foreground" : "bg-input"
        }`}
      >
        <span className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-background shadow-lg ring-0 transition-transform ${
          checked ? "translate-x-5" : "translate-x-0"
        }`} />
      </button>
    </div>
  );
}

// ── Default bodies ────────────────────────────────────────────────────────────

const DEFAULT_WEBHOOK_BODY = `{
  "alert_name": "{{alert_name}}",
  "alert_for": "{{alert_for}}",
  "alert_status": "{{alert_status}}",
  "alert_severity": "{{alert_severity}}",
  "alert_timestamp": "{{alert_timestamp}}",
  "is_resolved": {{is_resolved}},
  "is_triggered": {{is_triggered}}
}`;

const DEFAULT_SLACK_BODY = `{
  "text": "{{alert_name}} is {{alert_status}}"
}`;

// ── Type-specific config panels ───────────────────────────────────────────────

function WebhookConfig({
  url, setUrl, secret, setSecret, body, setBody, headers, setHeaders,
}: {
  url: string; setUrl: (v: string) => void;
  secret: string; setSecret: (v: string) => void;
  body: string; setBody: (v: string) => void;
  headers: Header[]; setHeaders: (h: Header[]) => void;
}) {
  function addHeader() { setHeaders([...headers, { key: "", value: "" }]); }
  function updateHeader(i: number, f: "key" | "value", v: string) {
    setHeaders(headers.map((h, idx) => idx === i ? { ...h, [f]: v } : h));
  }
  function removeHeader(i: number) { setHeaders(headers.filter((_, idx) => idx !== i)); }

  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">URL <span className="text-destructive">*</span></label>
        <input value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://example.com/webhook" className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Secret</label>
        <input value={secret} onChange={(e) => setSecret(e.target.value)}
          placeholder="Used to sign the payload via HMAC-SHA256 (optional)" className={inp} />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Headers</label>
        {headers.map((h, i) => (
          <div key={i} className="flex gap-2 items-center">
            <input placeholder="Header name" value={h.key}
              onChange={(e) => updateHeader(i, "key", e.target.value)} className={inp} />
            <input placeholder="Value" value={h.value}
              onChange={(e) => updateHeader(i, "value", e.target.value)} className={inp} />
            <button type="button" onClick={() => removeHeader(i)} className="text-muted-foreground hover:text-destructive shrink-0">
              <Trash2 size={15} />
            </button>
          </div>
        ))}
        <button type="button" onClick={addHeader}
          className="flex items-center gap-1.5 text-sm hover:underline self-start">
          <Plus size={13} /> Add Header
        </button>
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Custom Webhook Body</label>
        <p className="text-xs text-muted-foreground">Override the default JSON payload</p>
        <p className="text-xs text-muted-foreground">
          Use Mustache variables like <code className="font-mono">{`{{variable}}`}</code>. Available:{" "}
          <code className="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_timestamp, is_resolved, is_triggered</code>
        </p>
        <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={10}
          className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full" />
      </div>
    </>
  );
}

function EmailConfig({ to, setTo, from, setFrom, template, setTemplate }: {
  to: string; setTo: (v: string) => void;
  from: string; setFrom: (v: string) => void;
  template: string; setTemplate: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">To <span className="text-destructive">*</span></label>
        <input value={to} onChange={(e) => setTo(e.target.value)} placeholder="a@example.com, b@example.com" className={inp} required />
        <p className="text-xs text-muted-foreground">Comma-separated list of recipients</p>
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">From</label>
        <input type="email" value={from} onChange={(e) => setFrom(e.target.value)}
          placeholder="Uses the from address configured in Email settings" className={inp} />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">HTML Template</label>
        <textarea value={template} onChange={(e) => setTemplate(e.target.value)} rows={6}
          className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full" />
      </div>
    </>
  );
}

function SlackConfig({ url, setUrl, body, setBody }: {
  url: string; setUrl: (v: string) => void;
  body: string; setBody: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Webhook URL <span className="text-destructive">*</span></label>
        <input type="url" value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://hooks.slack.com/..." className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Body (JSON template)</label>
        <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={5}
          className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full" />
      </div>
    </>
  );
}

function PagerDutyConfig({ apiKey, setApiKey, severity, setSeverity }: {
  apiKey: string; setApiKey: (v: string) => void;
  severity: string; setSeverity: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Integration Key <span className="text-destructive">*</span></label>
        <input value={apiKey} onChange={(e) => setApiKey(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Severity</label>
        <select value={severity} onChange={(e) => setSeverity(e.target.value)} className={sel}>
          {["critical", "error", "warning", "info"].map((s) => <option key={s}>{s}</option>)}
        </select>
      </div>
    </>
  );
}

function MSTeamsConfig({ url, setUrl, body, setBody }: {
  url: string; setUrl: (v: string) => void;
  body: string; setBody: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Webhook URL <span className="text-destructive">*</span></label>
        <input type="url" value={url} onChange={(e) => setUrl(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Body</label>
        <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={5}
          className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full" />
      </div>
    </>
  );
}

function TelegramConfig({ token, setToken, chatId, setChatId, template, setTemplate }: {
  token: string; setToken: (v: string) => void;
  chatId: string; setChatId: (v: string) => void;
  template: string; setTemplate: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Bot Token <span className="text-destructive">*</span></label>
        <input value={token} onChange={(e) => setToken(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Chat ID <span className="text-destructive">*</span></label>
        <input value={chatId} onChange={(e) => setChatId(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Template</label>
        <textarea value={template} onChange={(e) => setTemplate(e.target.value)} rows={3}
          className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full" />
      </div>
    </>
  );
}

function TwilioConfig({ sid, setSid, token, setToken, from, setFrom, to, setTo, msg, setMsg }: {
  sid: string; setSid: (v: string) => void;
  token: string; setToken: (v: string) => void;
  from: string; setFrom: (v: string) => void;
  to: string; setTo: (v: string) => void;
  msg: string; setMsg: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Account SID <span className="text-destructive">*</span></label>
        <input value={sid} onChange={(e) => setSid(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Auth Token <span className="text-destructive">*</span></label>
        <input type="password" value={token} onChange={(e) => setToken(e.target.value)} className={inp} required />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">From <span className="text-destructive">*</span></label>
          <input value={from} onChange={(e) => setFrom(e.target.value)} placeholder="+15005550006" className={inp} required />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">To <span className="text-destructive">*</span></label>
          <input value={to} onChange={(e) => setTo(e.target.value)} placeholder="+15005550006" className={inp} required />
        </div>
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Message</label>
        <textarea value={msg} onChange={(e) => setMsg(e.target.value)} rows={3}
          className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full" />
      </div>
    </>
  );
}

function GoogleChatConfig({ url, setUrl, body, setBody }: {
  url: string; setUrl: (v: string) => void;
  body: string; setBody: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Webhook URL <span className="text-destructive">*</span></label>
        <input type="url" value={url} onChange={(e) => setUrl(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Body</label>
        <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={5}
          className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full" />
      </div>
    </>
  );
}

function DiscordConfig({ url, setUrl, username, setUsername, body, setBody }: {
  url: string; setUrl: (v: string) => void;
  username: string; setUsername: (v: string) => void;
  body: string; setBody: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Webhook URL <span className="text-destructive">*</span></label>
        <input type="url" value={url} onChange={(e) => setUrl(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Username</label>
        <input value={username} onChange={(e) => setUsername(e.target.value)} className={inp} />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Body</label>
        <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={5}
          className="rounded-lg border bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-ring resize-none w-full" />
      </div>
    </>
  );
}

function OpsgenieConfig({ apiKey, setApiKey, region, setRegion, priority, setPriority }: {
  apiKey: string; setApiKey: (v: string) => void;
  region: string; setRegion: (v: string) => void;
  priority: string; setPriority: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">API Key <span className="text-destructive">*</span></label>
        <input value={apiKey} onChange={(e) => setApiKey(e.target.value)} className={inp} required />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Region</label>
          <select value={region} onChange={(e) => setRegion(e.target.value)} className={sel}>
            {["US", "EU"].map((r) => <option key={r}>{r}</option>)}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Priority</label>
          <select value={priority} onChange={(e) => setPriority(e.target.value)} className={sel}>
            {["P1", "P2", "P3", "P4", "P5"].map((p) => <option key={p}>{p}</option>)}
          </select>
        </div>
      </div>
    </>
  );
}

function PushoverConfig({ userKey, setUserKey, appToken, setAppToken, priority, setPriority, device, setDevice }: {
  userKey: string; setUserKey: (v: string) => void;
  appToken: string; setAppToken: (v: string) => void;
  priority: string; setPriority: (v: string) => void;
  device: string; setDevice: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">User Key <span className="text-destructive">*</span></label>
        <input value={userKey} onChange={(e) => setUserKey(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">App Token <span className="text-destructive">*</span></label>
        <input value={appToken} onChange={(e) => setAppToken(e.target.value)} className={inp} required />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Priority</label>
          <select value={priority} onChange={(e) => setPriority(e.target.value)} className={sel}>
            {["-2", "-1", "0", "1", "2"].map((p) => <option key={p}>{p}</option>)}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Device</label>
          <input value={device} onChange={(e) => setDevice(e.target.value)} placeholder="Leave blank for all devices" className={inp} />
        </div>
      </div>
    </>
  );
}

function NtfyConfig({ url, setUrl, topic, setTopic, token, setToken, priority, setPriority, tags, setTags }: {
  url: string; setUrl: (v: string) => void;
  topic: string; setTopic: (v: string) => void;
  token: string; setToken: (v: string) => void;
  priority: string; setPriority: (v: string) => void;
  tags: string; setTags: (v: string) => void;
}) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">URL <span className="text-destructive">*</span></label>
        <input type="url" value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://ntfy.sh" className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Topic <span className="text-destructive">*</span></label>
        <input value={topic} onChange={(e) => setTopic(e.target.value)} className={inp} required />
      </div>
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Token</label>
        <input value={token} onChange={(e) => setToken(e.target.value)} className={inp} />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Priority</label>
          <select value={priority} onChange={(e) => setPriority(e.target.value)} className={sel}>
            {["1", "2", "3", "4", "5"].map((p) => <option key={p}>{p}</option>)}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Tags</label>
          <input value={tags} onChange={(e) => setTags(e.target.value)} placeholder="warning, rotating_light" className={inp} />
        </div>
      </div>
    </>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function ChannelFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const qc = useQueryClient();

  // Common
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [type, setType] = useState("Webhook");
  const [isActive, setIsActive] = useState(true);
  const [isGlobal, setIsGlobal] = useState(false);
  const [isLocked, setIsLocked] = useState(false);

  // Webhook
  const [whUrl, setWhUrl] = useState("");
  const [whSecret, setWhSecret] = useState("");
  const [whBody, setWhBody] = useState(DEFAULT_WEBHOOK_BODY);
  const [whHeaders, setWhHeaders] = useState<Header[]>([]);

  // Email
  const [emailTo, setEmailTo] = useState("");
  const [emailFrom, setEmailFrom] = useState("");
  const [emailTemplate, setEmailTemplate] = useState("");

  // Slack
  const [slackUrl, setSlackUrl] = useState("");
  const [slackBody, setSlackBody] = useState(DEFAULT_SLACK_BODY);

  // PagerDuty
  const [pdKey, setPdKey] = useState("");
  const [pdSeverity, setPdSeverity] = useState("critical");

  // MSTeams
  const [teamsUrl, setTeamsUrl] = useState("");
  const [teamsBody, setTeamsBody] = useState("");

  // Telegram
  const [tgToken, setTgToken] = useState("");
  const [tgChatId, setTgChatId] = useState("");
  const [tgTemplate, setTgTemplate] = useState("");

  // Twilio
  const [twSid, setTwSid] = useState("");
  const [twToken, setTwToken] = useState("");
  const [twFrom, setTwFrom] = useState("");
  const [twTo, setTwTo] = useState("");
  const [twMsg, setTwMsg] = useState("");

  // GoogleChat
  const [gcUrl, setGcUrl] = useState("");
  const [gcBody, setGcBody] = useState("");

  // Discord
  const [discordUrl, setDiscordUrl] = useState("");
  const [discordUsername, setDiscordUsername] = useState("");
  const [discordBody, setDiscordBody] = useState("");

  // Opsgenie
  const [ogKey, setOgKey] = useState("");
  const [ogRegion, setOgRegion] = useState("US");
  const [ogPriority, setOgPriority] = useState("P2");

  // Pushover
  const [poUserKey, setPoUserKey] = useState("");
  const [poAppToken, setPoAppToken] = useState("");
  const [poPriority, setPoPriority] = useState("0");
  const [poDevice, setPoDevice] = useState("");

  // Ntfy
  const [ntfyUrl, setNtfyUrl] = useState("");
  const [ntfyTopic, setNtfyTopic] = useState("");
  const [ntfyToken, setNtfyToken] = useState("");
  const [ntfyPriority, setNtfyPriority] = useState("3");
  const [ntfyTags, setNtfyTags] = useState("");

  // UI
  const [error, setError] = useState("");
  const [testResult, setTestResult] = useState<"success" | "error" | null>(null);
  const [testMsg, setTestMsg] = useState("");
  const [testing, setTesting] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState("");

  const { data: existing } = useQuery({
    queryKey: QUERY_KEYS.CHANNEL(id!),
    queryFn: () => channelsApi.get(id!),
    enabled: isEdit,
  });

  useEffect(() => {
    if (!existing) return;
    setName(existing.name);
    setIsActive(!existing.isInactive);
    setType(existing.type);
    setIsGlobal(existing.isGlobal);
    setIsLocked(existing.isLocked);
    setDescription(existing.description ?? "");
    const meta = existing.metaJson ? JSON.parse(existing.metaJson) : {};
    switch (existing.type) {
      case "Webhook":
        setWhUrl(meta.url ?? ""); setWhSecret(meta.secret ?? ""); setWhBody(meta.body ?? DEFAULT_WEBHOOK_BODY);
        setWhHeaders(meta.headers ? Object.entries(meta.headers).map(([k, v]) => ({ key: k, value: v as string })) : []);
        break;
      case "Email":
        setEmailTo(meta.to ?? ""); setEmailFrom(meta.from ?? ""); setEmailTemplate(meta.template ?? ""); break;
      case "Slack":
        setSlackUrl(meta.url ?? ""); setSlackBody(meta.body ?? DEFAULT_SLACK_BODY); break;
      case "PagerDuty":
        setPdKey(meta.integrationKey ?? ""); setPdSeverity(meta.severity ?? "critical"); break;
      case "MSTeams":
        setTeamsUrl(meta.url ?? ""); setTeamsBody(meta.body ?? ""); break;
      case "Telegram":
        setTgToken(meta.botToken ?? ""); setTgChatId(meta.chatId ?? ""); setTgTemplate(meta.template ?? ""); break;
      case "TwilioSms":
        setTwSid(meta.accountSid ?? ""); setTwToken(meta.authToken ?? ""); setTwFrom(meta.fromNumber ?? "");
        setTwTo(meta.toNumber ?? ""); setTwMsg(meta.message ?? ""); break;
      case "GoogleChat":
        setGcUrl(meta.url ?? ""); setGcBody(meta.body ?? ""); break;
      case "Discord":
        setDiscordUrl(meta.url ?? ""); setDiscordUsername(meta.username ?? ""); setDiscordBody(meta.body ?? ""); break;
      case "Opsgenie":
        setOgKey(meta.apiKey ?? ""); setOgRegion(meta.region ?? "US"); setOgPriority(meta.priority ?? "P2"); break;
      case "Pushover":
        setPoUserKey(meta.userKey ?? ""); setPoAppToken(meta.appToken ?? "");
        setPoPriority(meta.priority ?? "0"); setPoDevice(meta.device ?? ""); break;
      case "Ntfy":
        setNtfyUrl(meta.url ?? ""); setNtfyTopic(meta.topic ?? ""); setNtfyToken(meta.token ?? "");
        setNtfyPriority(meta.priority ?? "3"); setNtfyTags(meta.tags ?? ""); break;
    }
  }, [existing]);

  function buildMetaJson(): string {
    switch (type) {
      case "Webhook": {
        const headers: Record<string, string> = {};
        whHeaders.forEach(({ key, value }) => { if (key) headers[key] = value; });
        return JSON.stringify({ url: whUrl, secret: whSecret, body: whBody, headers });
      }
      case "Email": return JSON.stringify({ to: emailTo, from: emailFrom, template: emailTemplate });
      case "Slack": return JSON.stringify({ url: slackUrl, body: slackBody });
      case "PagerDuty": return JSON.stringify({ integrationKey: pdKey, severity: pdSeverity });
      case "MSTeams": return JSON.stringify({ url: teamsUrl, body: teamsBody });
      case "Telegram": return JSON.stringify({ botToken: tgToken, chatId: tgChatId, template: tgTemplate });
      case "TwilioSms": return JSON.stringify({ accountSid: twSid, authToken: twToken, fromNumber: twFrom, toNumber: twTo, message: twMsg });
      case "GoogleChat": return JSON.stringify({ url: gcUrl, body: gcBody });
      case "Discord": return JSON.stringify({ url: discordUrl, username: discordUsername, body: discordBody });
      case "Opsgenie": return JSON.stringify({ apiKey: ogKey, region: ogRegion, priority: ogPriority });
      case "Pushover": return JSON.stringify({ userKey: poUserKey, appToken: poAppToken, priority: poPriority, device: poDevice });
      case "Ntfy": return JSON.stringify({ url: ntfyUrl, topic: ntfyTopic, token: ntfyToken, priority: ntfyPriority, tags: ntfyTags });
      default: return "{}";
    }
  }

  function buildPayload() {
    return {
      name,
      type,
      description: description || undefined,
      isInactive: !isActive,
      metaJson: buildMetaJson(),
      isGlobal,
      isLocked: isGlobal ? isLocked : false,
    };
  }

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload = buildPayload();
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      if (isEdit) return channelsApi.update(id!, payload as any);
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      return channelsApi.create(payload as any);
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.CHANNELS });
      if (!isEdit && data && 'id' in (data as object)) {
        navigate(ROUTES.CHANNELS.DETAIL((data as { id: number }).id));
      }
    },
    onError: () => setError("Failed to save channel."),
  });

  const deleteMutation = useMutation({
    mutationFn: () => channelsApi.delete(id!),
    onSuccess: () => { qc.invalidateQueries({ queryKey: QUERY_KEYS.CHANNELS }); navigate(ROUTES.CHANNELS.LIST); },
    onError: () => setError("Failed to delete channel."),
  });

  async function handleTest() {
    setTesting(true);
    setTestResult(null);
    setTestMsg("");
    try {
      await channelsApi.test({ type, metaJson: buildMetaJson(), name: name || "Test Channel" });
      setTestResult("success");
      setTestMsg("Test notification sent successfully.");
    } catch {
      setTestResult("error");
      setTestMsg("Test notification failed. Check your configuration.");
    } finally {
      setTesting(false);
    }
  }

  const pageTitle = isEdit ? (existing?.name ?? "Edit Channel") : "New Channel";

  return (
    <AdminLayout title={pageTitle}>
      <div className="flex flex-col gap-6">
        {/* Breadcrumb */}
        <nav className="flex items-center gap-2 text-sm text-muted-foreground">
          <button onClick={() => navigate(ROUTES.CHANNELS.LIST)} className="hover:text-foreground transition-colors">
            Notification Channels
          </button>
          <span>/</span>
          <span className="text-foreground font-medium">{pageTitle}</span>
        </nav>

        {/* Title */}
        <div>
          <h1 className="text-xl font-bold">{pageTitle}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Configure notification triggers for your monitors</p>
        </div>

        {/* Feedback banners */}
        {error && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">{error}</div>
        )}
        {testResult && (
          <div className={`rounded-lg border px-4 py-3 text-sm ${
            testResult === "success"
              ? "border-green-200 bg-green-50 text-green-700"
              : "border-destructive/20 bg-destructive/5 text-destructive"
          }`}>{testMsg}</div>
        )}

        {/* Main card */}
        <div className="rounded-xl border bg-card">
          {/* Trigger type */}
          <div className="px-6 pt-6 pb-4 border-b border-border">
            <p className="text-sm font-semibold mb-1">Trigger Type</p>
            <p className="text-xs text-muted-foreground mb-3">Select the type of notification to send</p>
            <Select value={type} onValueChange={setType} disabled={isEdit}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CHANNEL_TYPES.map((t) => (
                  <SelectItem key={t} value={t}>{CHANNEL_TYPE_LABELS[t] ?? t}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            {isEdit && <p className="text-xs text-muted-foreground mt-1.5">Type cannot be changed after creation.</p>}
          </div>

          {/* Status + Global toggles */}
          <div className="px-6">
            <ToggleRow
              label="Status"
              description="Enable or disable this trigger"
              checked={isActive}
              onChange={setIsActive}
            />
            <ToggleRow
              label="Global"
              description="Automatically apply this trigger to all existing and future alert configs"
              checked={isGlobal}
              onChange={setIsGlobal}
            />
            {isGlobal && (
              <ToggleRow
                label="Locked"
                description="Prevent this trigger from being removed from alert configs"
                checked={isLocked}
                onChange={setIsLocked}
              />
            )}
          </div>

          {/* Common fields + type-specific */}
          <div className="px-6 py-6 flex flex-col gap-5">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">Name <span className="text-destructive">*</span></label>
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="My Trigger" className={inp} required />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">Description</label>
              <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Optional description" className={inp} />
            </div>

            {/* Type-specific */}
            {type === "Webhook"    && <WebhookConfig url={whUrl} setUrl={setWhUrl} secret={whSecret} setSecret={setWhSecret} body={whBody} setBody={setWhBody} headers={whHeaders} setHeaders={setWhHeaders} />}
            {type === "Email"      && <EmailConfig to={emailTo} setTo={setEmailTo} from={emailFrom} setFrom={setEmailFrom} template={emailTemplate} setTemplate={setEmailTemplate} />}
            {type === "Slack"      && <SlackConfig url={slackUrl} setUrl={setSlackUrl} body={slackBody} setBody={setSlackBody} />}
            {type === "PagerDuty"  && <PagerDutyConfig apiKey={pdKey} setApiKey={setPdKey} severity={pdSeverity} setSeverity={setPdSeverity} />}
            {type === "MSTeams"    && <MSTeamsConfig url={teamsUrl} setUrl={setTeamsUrl} body={teamsBody} setBody={setTeamsBody} />}
            {type === "Telegram"   && <TelegramConfig token={tgToken} setToken={setTgToken} chatId={tgChatId} setChatId={setTgChatId} template={tgTemplate} setTemplate={setTgTemplate} />}
            {type === "TwilioSms"  && <TwilioConfig sid={twSid} setSid={setTwSid} token={twToken} setToken={setTwToken} from={twFrom} setFrom={setTwFrom} to={twTo} setTo={setTwTo} msg={twMsg} setMsg={setTwMsg} />}
            {type === "GoogleChat" && <GoogleChatConfig url={gcUrl} setUrl={setGcUrl} body={gcBody} setBody={setGcBody} />}
            {type === "Discord"    && <DiscordConfig url={discordUrl} setUrl={setDiscordUrl} username={discordUsername} setUsername={setDiscordUsername} body={discordBody} setBody={setDiscordBody} />}
            {type === "Opsgenie"   && <OpsgenieConfig apiKey={ogKey} setApiKey={setOgKey} region={ogRegion} setRegion={setOgRegion} priority={ogPriority} setPriority={setOgPriority} />}
            {type === "Pushover"   && <PushoverConfig userKey={poUserKey} setUserKey={setPoUserKey} appToken={poAppToken} setAppToken={setPoAppToken} priority={poPriority} setPriority={setPoPriority} device={poDevice} setDevice={setPoDevice} />}
            {type === "Ntfy"       && <NtfyConfig url={ntfyUrl} setUrl={setNtfyUrl} topic={ntfyTopic} setTopic={setNtfyTopic} token={ntfyToken} setToken={setNtfyToken} priority={ntfyPriority} setPriority={setNtfyPriority} tags={ntfyTags} setTags={setNtfyTags} />}
          </div>

          {/* Footer */}
          <div className="flex items-center justify-between px-6 py-4 border-t border-border">
            <button
              type="button"
              onClick={handleTest}
              disabled={testing}
              className="flex items-center gap-2 rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors"
            >
              <FlaskConical size={14} />
              {testing ? "Testing…" : "Test Trigger"}
            </button>
            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={() => navigate(ROUTES.CHANNELS.LIST)}
                className="rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted transition-colors"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={() => { setError(""); saveMutation.mutate(); }}
                disabled={saveMutation.isPending || !name}
                className="rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
              >
                {saveMutation.isPending ? "Saving…" : isEdit ? "Save changes" : "Create Trigger"}
              </button>
            </div>
          </div>
        </div>

        {/* Danger Zone */}
        {isEdit && (
          <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6 flex flex-col gap-4">
            <p className="text-sm">
              Permanently delete this channel. Type{" "}
              <code className="font-mono font-semibold">{existing?.name}</code> to confirm.
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
                {deleteMutation.isPending ? "Deleting…" : "Delete Channel"}
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
