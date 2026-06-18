import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, CheckCircle, AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { channelsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const CHANNEL_TYPES = [
  "Webhook", "Email", "Slack", "PagerDuty", "MSTeams", "Telegram",
  "TwilioSms", "GoogleChat", "Discord", "Opsgenie", "Pushover", "Ntfy",
];

interface Header { key: string; value: string }

function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <label className="flex items-center gap-2 cursor-pointer">
      <div className="relative">
        <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="sr-only peer" />
        <div className="w-9 h-5 rounded-full bg-gray-200 peer-checked:bg-indigo-600 transition-colors" />
        <div className="absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white shadow transition-transform peer-checked:translate-x-4" />
      </div>
      <span className="text-sm font-medium">{label}</span>
    </label>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-sm font-medium">{label}</label>
      {children}
    </div>
  );
}

const inputCls = "rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500";
const selectCls = "rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500";
const textareaCls = "rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500";

// Default JSON bodies
const DEFAULT_WEBHOOK_BODY = `{
  "text": "{{message}}"
}`;
const DEFAULT_SLACK_BODY = `{
  "text": "{{message}}"
}`;

export default function ChannelFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const qc = useQueryClient();

  // Common fields
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

  // UI state
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");
  const [testResult, setTestResult] = useState<"success" | "error" | null>(null);
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
    setIsActive(existing.isActive);
    setType(existing.type);
    const cfg = existing.config as Record<string, unknown>;
    setIsGlobal((cfg?.isGlobal as boolean) ?? false);
    setIsLocked((cfg?.isLocked as boolean) ?? false);
    setDescription((cfg?.description as string) ?? "");

    const meta = cfg?.metaJson ? JSON.parse(cfg.metaJson as string) : {};
    switch (existing.type) {
      case "Webhook":
        setWhUrl(meta.url ?? "");
        setWhSecret(meta.secret ?? "");
        setWhBody(meta.body ?? DEFAULT_WEBHOOK_BODY);
        setWhHeaders(meta.headers ? Object.entries(meta.headers).map(([k, v]) => ({ key: k, value: v as string })) : []);
        break;
      case "Email":
        setEmailTo(meta.to ?? "");
        setEmailFrom(meta.from ?? "");
        setEmailTemplate(meta.template ?? "");
        break;
      case "Slack":
        setSlackUrl(meta.url ?? "");
        setSlackBody(meta.body ?? DEFAULT_SLACK_BODY);
        break;
      case "PagerDuty":
        setPdKey(meta.integrationKey ?? "");
        setPdSeverity(meta.severity ?? "critical");
        break;
      case "MSTeams":
        setTeamsUrl(meta.url ?? "");
        setTeamsBody(meta.body ?? "");
        break;
      case "Telegram":
        setTgToken(meta.botToken ?? "");
        setTgChatId(meta.chatId ?? "");
        setTgTemplate(meta.template ?? "");
        break;
      case "TwilioSms":
        setTwSid(meta.accountSid ?? "");
        setTwToken(meta.authToken ?? "");
        setTwFrom(meta.fromNumber ?? "");
        setTwTo(meta.toNumber ?? "");
        setTwMsg(meta.message ?? "");
        break;
      case "GoogleChat":
        setGcUrl(meta.url ?? "");
        setGcBody(meta.body ?? "");
        break;
      case "Discord":
        setDiscordUrl(meta.url ?? "");
        setDiscordUsername(meta.username ?? "");
        setDiscordBody(meta.body ?? "");
        break;
      case "Opsgenie":
        setOgKey(meta.apiKey ?? "");
        setOgRegion(meta.region ?? "US");
        setOgPriority(meta.priority ?? "P2");
        break;
      case "Pushover":
        setPoUserKey(meta.userKey ?? "");
        setPoAppToken(meta.appToken ?? "");
        setPoPriority(meta.priority ?? "0");
        setPoDevice(meta.device ?? "");
        break;
      case "Ntfy":
        setNtfyUrl(meta.url ?? "");
        setNtfyTopic(meta.topic ?? "");
        setNtfyToken(meta.token ?? "");
        setNtfyPriority(meta.priority ?? "3");
        setNtfyTags(meta.tags ?? "");
        break;
    }
  }, [existing]);

  function buildMetaJson(): string {
    switch (type) {
      case "Webhook": {
        const headers: Record<string, string> = {};
        whHeaders.forEach(({ key, value }) => { if (key) headers[key] = value; });
        return JSON.stringify({ url: whUrl, secret: whSecret, body: whBody, headers });
      }
      case "Email":
        return JSON.stringify({ to: emailTo, from: emailFrom, template: emailTemplate });
      case "Slack":
        return JSON.stringify({ url: slackUrl, body: slackBody });
      case "PagerDuty":
        return JSON.stringify({ integrationKey: pdKey, severity: pdSeverity });
      case "MSTeams":
        return JSON.stringify({ url: teamsUrl, body: teamsBody });
      case "Telegram":
        return JSON.stringify({ botToken: tgToken, chatId: tgChatId, template: tgTemplate });
      case "TwilioSms":
        return JSON.stringify({ accountSid: twSid, authToken: twToken, fromNumber: twFrom, toNumber: twTo, message: twMsg });
      case "GoogleChat":
        return JSON.stringify({ url: gcUrl, body: gcBody });
      case "Discord":
        return JSON.stringify({ url: discordUrl, username: discordUsername, body: discordBody });
      case "Opsgenie":
        return JSON.stringify({ apiKey: ogKey, region: ogRegion, priority: ogPriority });
      case "Pushover":
        return JSON.stringify({ userKey: poUserKey, appToken: poAppToken, priority: poPriority, device: poDevice });
      case "Ntfy":
        return JSON.stringify({ url: ntfyUrl, topic: ntfyTopic, token: ntfyToken, priority: ntfyPriority, tags: ntfyTags });
      default:
        return "{}";
    }
  }

  function buildPayload() {
    return {
      name,
      type,
      description,
      status: isActive ? "ACTIVE" : "INACTIVE",
      metaJson: buildMetaJson(),
      isGlobal,
      isLocked: isGlobal ? isLocked : false,
    };
  }

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload = buildPayload();
      if (isEdit) return channelsApi.update(id!, payload as unknown as Parameters<typeof channelsApi.update>[1]);
      return channelsApi.create(payload as unknown as Parameters<typeof channelsApi.create>[0]);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.CHANNELS });
      setSuccess(true);
      setError("");
      setTimeout(() => setSuccess(false), 3000);
    },
    onError: () => setError("Failed to save channel."),
  });

  const deleteMutation = useMutation({
    mutationFn: () => channelsApi.delete(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.CHANNELS });
      navigate(ROUTES.CHANNELS.LIST);
    },
    onError: () => setError("Failed to delete channel."),
  });

  async function handleTest() {
    setTesting(true);
    setTestResult(null);
    try {
      await channelsApi.test({ type, config: { metaJson: buildMetaJson() } });
      setTestResult("success");
    } catch {
      setTestResult("error");
    } finally {
      setTesting(false);
    }
  }

  function addHeader() {
    setWhHeaders((h) => [...h, { key: "", value: "" }]);
  }

  function updateHeader(i: number, field: "key" | "value", val: string) {
    setWhHeaders((h) => h.map((hdr, idx) => idx === i ? { ...hdr, [field]: val } : hdr));
  }

  function removeHeader(i: number) {
    setWhHeaders((h) => h.filter((_, idx) => idx !== i));
  }

  const title = isEdit ? `Edit Channel${existing ? ` — ${existing.name}` : ""}` : "New Channel";

  return (
    <AdminLayout title={title}>
      <div className="max-w-2xl flex flex-col gap-6">
        <form
          onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }}
          className="flex flex-col gap-5"
        >
          {success && (
            <div className="flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
              <CheckCircle size={16} /> Saved successfully.
            </div>
          )}
          {error && (
            <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <AlertCircle size={16} /> {error}
            </div>
          )}
          {testResult === "success" && (
            <div className="flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
              <CheckCircle size={16} /> Test notification sent successfully.
            </div>
          )}
          {testResult === "error" && (
            <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <AlertCircle size={16} /> Test notification failed.
            </div>
          )}

          {/* Common fields */}
          <Field label="Name">
            <input type="text" value={name} onChange={(e) => setName(e.target.value)} required className={inputCls} />
          </Field>

          <Field label="Description">
            <input type="text" value={description} onChange={(e) => setDescription(e.target.value)} className={inputCls} />
          </Field>

          <Field label="Type">
            <select
              value={type}
              onChange={(e) => setType(e.target.value)}
              disabled={isEdit}
              className={selectCls}
            >
              {CHANNEL_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
            {isEdit && <p className="text-xs text-gray-400">Type cannot be changed after creation.</p>}
          </Field>

          <Toggle checked={isActive} onChange={setIsActive} label="Active" />
          <Toggle checked={isGlobal} onChange={setIsGlobal} label="Global (send to all checks)" />
          {isGlobal && <Toggle checked={isLocked} onChange={setIsLocked} label="Locked" />}

          {/* Type-specific config */}
          <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 flex flex-col gap-4">
            <h3 className="text-sm font-semibold text-gray-700">{type} Configuration</h3>

            {type === "Webhook" && (
              <>
                <Field label="URL *">
                  <input type="url" value={whUrl} onChange={(e) => setWhUrl(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Secret">
                  <input type="text" value={whSecret} onChange={(e) => setWhSecret(e.target.value)} className={inputCls} />
                </Field>
                <Field label="Body (JSON template)">
                  <textarea value={whBody} onChange={(e) => setWhBody(e.target.value)} rows={5} className={textareaCls} />
                </Field>
                <div className="flex flex-col gap-2">
                  <label className="text-sm font-medium">Headers</label>
                  {whHeaders.map((h, i) => (
                    <div key={i} className="flex gap-2 items-center">
                      <input
                        type="text"
                        placeholder="Header name"
                        value={h.key}
                        onChange={(e) => updateHeader(i, "key", e.target.value)}
                        className={`flex-1 ${inputCls}`}
                      />
                      <input
                        type="text"
                        placeholder="Value"
                        value={h.value}
                        onChange={(e) => updateHeader(i, "value", e.target.value)}
                        className={`flex-1 ${inputCls}`}
                      />
                      <button type="button" onClick={() => removeHeader(i)} className="text-gray-400 hover:text-red-600">
                        <Trash2 size={15} />
                      </button>
                    </div>
                  ))}
                  <button type="button" onClick={addHeader} className="flex items-center gap-1.5 text-sm text-indigo-600 hover:text-indigo-800 self-start">
                    <Plus size={14} /> Add Header
                  </button>
                </div>
              </>
            )}

            {type === "Email" && (
              <>
                <Field label="To (comma-separated) *">
                  <input type="text" value={emailTo} onChange={(e) => setEmailTo(e.target.value)} required className={inputCls} placeholder="a@example.com, b@example.com" />
                </Field>
                <Field label="From">
                  <input type="email" value={emailFrom} onChange={(e) => setEmailFrom(e.target.value)} className={inputCls} />
                </Field>
                <Field label="HTML Template">
                  <textarea value={emailTemplate} onChange={(e) => setEmailTemplate(e.target.value)} rows={5} className={textareaCls} />
                </Field>
              </>
            )}

            {type === "Slack" && (
              <>
                <Field label="Webhook URL *">
                  <input type="url" value={slackUrl} onChange={(e) => setSlackUrl(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Body (JSON template)">
                  <textarea value={slackBody} onChange={(e) => setSlackBody(e.target.value)} rows={5} className={textareaCls} />
                </Field>
              </>
            )}

            {type === "PagerDuty" && (
              <>
                <Field label="Integration Key *">
                  <input type="text" value={pdKey} onChange={(e) => setPdKey(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Severity">
                  <select value={pdSeverity} onChange={(e) => setPdSeverity(e.target.value)} className={selectCls}>
                    {["critical", "error", "warning", "info"].map((s) => <option key={s} value={s}>{s}</option>)}
                  </select>
                </Field>
              </>
            )}

            {type === "MSTeams" && (
              <>
                <Field label="Webhook URL *">
                  <input type="url" value={teamsUrl} onChange={(e) => setTeamsUrl(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Body">
                  <textarea value={teamsBody} onChange={(e) => setTeamsBody(e.target.value)} rows={5} className={textareaCls} />
                </Field>
              </>
            )}

            {type === "Telegram" && (
              <>
                <Field label="Bot Token *">
                  <input type="text" value={tgToken} onChange={(e) => setTgToken(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Chat ID *">
                  <input type="text" value={tgChatId} onChange={(e) => setTgChatId(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Template">
                  <textarea value={tgTemplate} onChange={(e) => setTgTemplate(e.target.value)} rows={3} className={textareaCls} />
                </Field>
              </>
            )}

            {type === "TwilioSms" && (
              <>
                <Field label="Account SID *">
                  <input type="text" value={twSid} onChange={(e) => setTwSid(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Auth Token *">
                  <input type="password" value={twToken} onChange={(e) => setTwToken(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="From Number *">
                  <input type="text" value={twFrom} onChange={(e) => setTwFrom(e.target.value)} required className={inputCls} placeholder="+15005550006" />
                </Field>
                <Field label="To Number *">
                  <input type="text" value={twTo} onChange={(e) => setTwTo(e.target.value)} required className={inputCls} placeholder="+15005550006" />
                </Field>
                <Field label="Message">
                  <textarea value={twMsg} onChange={(e) => setTwMsg(e.target.value)} rows={3} className={textareaCls} />
                </Field>
              </>
            )}

            {type === "GoogleChat" && (
              <>
                <Field label="Webhook URL *">
                  <input type="url" value={gcUrl} onChange={(e) => setGcUrl(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Body">
                  <textarea value={gcBody} onChange={(e) => setGcBody(e.target.value)} rows={5} className={textareaCls} />
                </Field>
              </>
            )}

            {type === "Discord" && (
              <>
                <Field label="Webhook URL *">
                  <input type="url" value={discordUrl} onChange={(e) => setDiscordUrl(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Username">
                  <input type="text" value={discordUsername} onChange={(e) => setDiscordUsername(e.target.value)} className={inputCls} />
                </Field>
                <Field label="Body">
                  <textarea value={discordBody} onChange={(e) => setDiscordBody(e.target.value)} rows={5} className={textareaCls} />
                </Field>
              </>
            )}

            {type === "Opsgenie" && (
              <>
                <Field label="API Key *">
                  <input type="text" value={ogKey} onChange={(e) => setOgKey(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Region">
                  <select value={ogRegion} onChange={(e) => setOgRegion(e.target.value)} className={selectCls}>
                    {["US", "EU"].map((r) => <option key={r} value={r}>{r}</option>)}
                  </select>
                </Field>
                <Field label="Priority">
                  <select value={ogPriority} onChange={(e) => setOgPriority(e.target.value)} className={selectCls}>
                    {["P1", "P2", "P3", "P4", "P5"].map((p) => <option key={p} value={p}>{p}</option>)}
                  </select>
                </Field>
              </>
            )}

            {type === "Pushover" && (
              <>
                <Field label="User Key *">
                  <input type="text" value={poUserKey} onChange={(e) => setPoUserKey(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="App Token *">
                  <input type="text" value={poAppToken} onChange={(e) => setPoAppToken(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Priority">
                  <select value={poPriority} onChange={(e) => setPoPriority(e.target.value)} className={selectCls}>
                    {["-2", "-1", "0", "1", "2"].map((p) => <option key={p} value={p}>{p}</option>)}
                  </select>
                </Field>
                <Field label="Device">
                  <input type="text" value={poDevice} onChange={(e) => setPoDevice(e.target.value)} className={inputCls} placeholder="Optional — leave blank for all devices" />
                </Field>
              </>
            )}

            {type === "Ntfy" && (
              <>
                <Field label="URL *">
                  <input type="url" value={ntfyUrl} onChange={(e) => setNtfyUrl(e.target.value)} required className={inputCls} placeholder="https://ntfy.sh" />
                </Field>
                <Field label="Topic *">
                  <input type="text" value={ntfyTopic} onChange={(e) => setNtfyTopic(e.target.value)} required className={inputCls} />
                </Field>
                <Field label="Token">
                  <input type="text" value={ntfyToken} onChange={(e) => setNtfyToken(e.target.value)} className={inputCls} />
                </Field>
                <Field label="Priority">
                  <select value={ntfyPriority} onChange={(e) => setNtfyPriority(e.target.value)} className={selectCls}>
                    {["1", "2", "3", "4", "5"].map((p) => <option key={p} value={p}>{p}</option>)}
                  </select>
                </Field>
                <Field label="Tags">
                  <input type="text" value={ntfyTags} onChange={(e) => setNtfyTags(e.target.value)} className={inputCls} placeholder="warning, rotating_light" />
                </Field>
              </>
            )}
          </div>

          <div className="flex items-center gap-3">
            <button
              type="submit"
              disabled={saveMutation.isPending}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
            >
              {saveMutation.isPending ? "Saving…" : isEdit ? "Save Changes" : "Create Channel"}
            </button>
            <button
              type="button"
              onClick={handleTest}
              disabled={testing}
              className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
            >
              {testing ? "Testing…" : "Send Test"}
            </button>
            <button
              type="button"
              onClick={() => navigate(ROUTES.CHANNELS.LIST)}
              className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              Cancel
            </button>
          </div>
        </form>

        {/* Danger zone */}
        {isEdit && (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4 flex flex-col gap-3">
            <h3 className="text-sm font-semibold text-red-700">Danger Zone</h3>
            <p className="text-sm text-red-600">
              Delete this channel permanently. Type the channel name to confirm.
            </p>
            <div className="flex gap-2">
              <input
                type="text"
                value={deleteConfirm}
                onChange={(e) => setDeleteConfirm(e.target.value)}
                placeholder={existing?.name ?? ""}
                className="flex-1 rounded-md border border-red-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-500"
              />
              <button
                type="button"
                disabled={deleteConfirm !== existing?.name || deleteMutation.isPending}
                onClick={() => deleteMutation.mutate()}
                className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              >
                {deleteMutation.isPending ? "Deleting…" : "Delete"}
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
