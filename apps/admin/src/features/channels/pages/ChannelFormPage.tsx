import { useState, useEffect } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { FlaskConical, ExternalLink } from "lucide-react";
import { Icon } from "@iconify/react";
import { AdminLayout } from "@/components/AdminLayout";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { channelsApi, integrationsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { CHANNEL_TYPE_MAP, CHANNEL_TYPES } from "@/constants/channels";

// ── Styles ────────────────────────────────────────────────────────────────────

const inp = "rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";

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

// ── Page ──────────────────────────────────────────────────────────────────────

export default function ChannelFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [type, setType] = useState("Slack");
  const [isActive, setIsActive] = useState(true);
  const [isGlobal, setIsGlobal] = useState(false);
  const [isLocked, setIsLocked] = useState(false);
  const [integrationId, setIntegrationId] = useState<number | null>(null);
  // Target — the destination within the integration (chat ID, channel, number, etc.)
  const [target, setTarget] = useState("");

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

  // All integrations, filtered to the selected type
  const { data: allIntegrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });

  const integrations = allIntegrations.filter((i) => i.type === type);

  useEffect(() => {
    if (!existing) return;
    setName(existing.name);
    setIsActive(!existing.isInactive);
    setType(existing.type);
    setIsGlobal(existing.isGlobal);
    setIsLocked(existing.isLocked);
    setDescription(existing.description ?? "");
    setIntegrationId(existing.integrationId ?? null);
    try {
      const meta = existing.metaJson ? JSON.parse(existing.metaJson) : {};
      setTarget(meta.target ?? meta.chatId ?? meta.channel ?? meta.toNumber ?? meta.to ?? meta.userKey ?? meta.topic ?? "");
    } catch { /* ignore */ }
  }, [existing]);

  // Derive the effective integration id: auto-select when only one option exists
  const effectiveIntegrationId =
    !isEdit && integrationId === null && integrations.length === 1
      ? integrations[0].id
      : integrationId;

  function buildMetaJson(): string {
    const t = target.trim();
    if (!t) return "{}";
    switch (type) {
      case "Telegram":   return JSON.stringify({ chatId: t });
      case "Slack":      return JSON.stringify({ channel: t });
      case "Discord":    return JSON.stringify({ channelId: t });
      case "Email":      return JSON.stringify({ to: t });
      case "TwilioSms":  return JSON.stringify({ toNumber: t });
      case "Pushover":   return JSON.stringify({ userKey: t });
      case "Ntfy":       return JSON.stringify({ topic: t });
      case "GoogleChat": return JSON.stringify({ spaceId: t });
      case "MSTeams":    return JSON.stringify({ channel: t });
      case "PagerDuty":  return JSON.stringify({ routingKey: t });
      case "Opsgenie":   return JSON.stringify({ responders: t });
      default:           return JSON.stringify({ target: t });
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
      integrationId: effectiveIntegrationId ?? undefined,
    };
  }

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload = buildPayload();
      if (isEdit) return channelsApi.update(id!, payload);
      return channelsApi.create(payload);
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.CHANNELS });
      if (!isEdit && data && "id" in (data as object)) {
        navigate(ROUTES.CHANNELS.DETAIL((data as { id: number }).id));
      }
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
    setTestMsg("");
    try {
      await channelsApi.test({
        type,
        metaJson: buildMetaJson(),
        name: name || "Test Channel",
        integrationId: effectiveIntegrationId ?? undefined,
      });
      setTestResult("success");
      setTestMsg("Test notification sent successfully.");
    } catch (err: unknown) {
      setTestResult("error");
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setTestMsg(msg ?? "Test notification failed. Check your configuration.");
    } finally {
      setTesting(false);
    }
  }

  const pageTitle = isEdit ? (existing?.name ?? "Edit Channel") : "New Channel";
  const selectedIntegration = integrations.find((i) => i.id === effectiveIntegrationId);
  const typeMeta = CHANNEL_TYPE_MAP[type as keyof typeof CHANNEL_TYPE_MAP];

  const TARGET_CONFIG: Record<string, { label: string; placeholder: string; hint?: string; required?: boolean }> = {
    Telegram:   { label: "Chat ID",       placeholder: "-1001234567890",       hint: "Group, channel or user chat ID. Forward a message to @userinfobot to find it.", required: true },
    Slack:      { label: "Channel",        placeholder: "#alerts",              hint: "Channel name (with #) or user ID for DMs.", required: true },
    Discord:    { label: "Channel ID",     placeholder: "123456789012345678",   hint: "Right-click the channel → Copy Channel ID.", required: true },
    Email:      { label: "To",             placeholder: "ops@example.com",      hint: "Comma-separated recipients. Uses the system email provider.", required: true },
    TwilioSms:  { label: "To number",      placeholder: "+15005550006",         hint: "E.164 format destination number.", required: true },
    Pushover:   { label: "User Key",       placeholder: "uQiRzpo4DXghDmr9QzzfQu", hint: "Found in your Pushover dashboard.", required: true },
    Ntfy:       { label: "Topic",          placeholder: "my-alerts",            hint: "The topic name to publish to.", required: true },
    GoogleChat: { label: "Space ID",       placeholder: "AAAA…",               hint: "From the space URL: chat.google.com/room/{spaceId}", required: true },
    MSTeams:    { label: "Channel",        placeholder: "General",              hint: "Optional: the channel name within the team." },
    PagerDuty:  { label: "Routing Key override", placeholder: "Leave blank to use integration key", hint: "Optional: override the routing key from the integration." },
    Opsgenie:   { label: "Responders",     placeholder: "team:ops, user:john",  hint: "Optional: comma-separated team/user responders." },
    Webhook:    { label: "URL override",   placeholder: "https://…",            hint: "Optional: override the URL set in the integration." },
  };

  const canTest = type === "Email"
    ? !!target.trim()
    : !!effectiveIntegrationId && (!TARGET_CONFIG[type]?.required || !!target.trim());

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

        <div>
          <h1 className="text-xl font-bold">{pageTitle}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Configure notification triggers for your monitors</p>
        </div>

        {error && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">{error}</div>
        )}
        {testResult && (
          <div className={`rounded-lg border px-4 py-3 text-sm ${
            testResult === "success"
              ? "border-green-500/30 bg-green-500/10 text-green-600 dark:text-green-400"
              : "border-destructive/20 bg-destructive/5 text-destructive"
          }`}>{testMsg}</div>
        )}

        <div className="rounded-xl border bg-card">
          {/* Trigger type */}
          <div className="px-6 pt-6 pb-4 border-b border-border">
            <p className="text-sm font-semibold mb-1">Trigger Type</p>
            <p className="text-xs text-muted-foreground mb-3">Select the type of notification to send</p>
            <Select value={type} onValueChange={(v) => { if (v) { setType(v); if (!isEdit) setIntegrationId(null); } }} disabled={isEdit}>
              <SelectTrigger className="w-56">
                <SelectValue>
                  {typeMeta ? (
                    <span className="inline-flex items-center gap-2">
                      <Icon icon={typeMeta.icon} className={`size-4 ${typeMeta.iconClass ?? ""}`} />
                      {typeMeta.label}
                    </span>
                  ) : type}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {CHANNEL_TYPES.map((t) => (
                  <SelectItem key={t.value} value={t.value}>
                    <span className="inline-flex items-center gap-2">
                      <Icon icon={t.icon} className={`size-4 ${t.iconClass ?? ""}`} />
                      {t.label}
                      {t.alpha && (
                        <span className="ml-auto rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400 px-1.5 py-0.5 text-[10px] font-medium">
                          Alpha
                        </span>
                      )}
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {isEdit && <p className="text-xs text-muted-foreground mt-1.5">Type cannot be changed after creation.</p>}
          </div>

          {/* Status + Global toggles */}
          <div className="px-6">
            <ToggleRow label="Status" description="Enable or disable this trigger" checked={isActive} onChange={setIsActive} />
            <ToggleRow label="Global" description="Automatically apply this trigger to all existing and future alert configs" checked={isGlobal} onChange={setIsGlobal} />
            {isGlobal && (
              <ToggleRow label="Locked" description="Prevent this trigger from being removed from alert configs" checked={isLocked} onChange={setIsLocked} />
            )}
          </div>

          {/* Common fields */}
          <div className="px-6 py-6 flex flex-col gap-5">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">Name <span className="text-destructive">*</span></label>
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="My Trigger" className={inp} required />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">Description</label>
              <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Optional description" className={inp} />
            </div>

            {/* Target — destination within the integration */}
            {TARGET_CONFIG[type] && (
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold">
                  {TARGET_CONFIG[type].label}
                  {TARGET_CONFIG[type].required && <span className="text-destructive ml-0.5">*</span>}
                </label>
                <input
                  value={target}
                  onChange={(e) => setTarget(e.target.value)}
                  placeholder={TARGET_CONFIG[type].placeholder}
                  className={inp}
                />
                {TARGET_CONFIG[type].hint && (
                  <p className="text-xs text-muted-foreground">{TARGET_CONFIG[type].hint}</p>
                )}
              </div>
            )}

            {/* Integration selector — Email uses the system email service, no integration needed */}
            {type === "Email" ? (
              <div className="rounded-lg border border-border bg-muted/30 px-4 py-3 flex items-start gap-3">
                <Icon icon="lucide:mail" className="size-4 mt-0.5 text-muted-foreground shrink-0" />
                <div>
                  <p className="text-sm font-medium text-foreground">Uses system email</p>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Email channels use the provider configured in{" "}
                    <button
                      type="button"
                      onClick={() => navigate(ROUTES.CONFIG.EMAIL)}
                      className="text-blue-600 hover:underline"
                    >
                      Settings → Email
                    </button>
                    . No integration required.
                  </p>
                </div>
              </div>
            ) : (
              <div className="flex flex-col gap-1.5">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-semibold">
                    Integration <span className="text-destructive">*</span>
                  </label>
                  <Link
                    to={`${ROUTES.INTEGRATIONS.NEW}?provider=${type}`}
                    className="flex items-center gap-1 text-xs text-blue-600 hover:text-blue-700"
                  >
                    <ExternalLink size={11} /> Add integration
                  </Link>
                </div>
                <p className="text-xs text-muted-foreground">
                  Select a {typeMeta?.label ?? type} integration that provides the credentials for this channel.
                </p>

                {integrations.length === 0 ? (
                  <div className="rounded-lg border border-dashed border-border bg-muted/30 px-4 py-5 text-center">
                    <p className="text-sm text-muted-foreground">
                      No {typeMeta?.label ?? type} integrations configured yet.
                    </p>
                    <Link
                      to={`${ROUTES.INTEGRATIONS.NEW}?provider=${type}`}
                      className="mt-2 text-sm text-blue-600 hover:text-blue-700 font-medium"
                    >
                      Create one now →
                    </Link>
                  </div>
                ) : (
                  <div className="grid gap-2">
                    {integrations.map((integration) => (
                      <button
                        key={integration.id}
                        type="button"
                        onClick={() => setIntegrationId(integration.id)}
                        className={`flex items-center gap-3 rounded-lg border px-4 py-3 text-left transition-colors ${
                          effectiveIntegrationId === integration.id
                            ? "border-foreground bg-muted/50"
                            : "border-border hover:border-foreground/40 hover:bg-muted/30"
                        }`}
                      >
                        {typeMeta && (
                          <Icon icon={typeMeta.icon} className={`size-5 shrink-0 ${typeMeta.iconClass ?? ""}`} />
                        )}
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium text-foreground truncate">{integration.name}</p>
                          {integration.description && (
                            <p className="text-xs text-muted-foreground truncate">{integration.description}</p>
                          )}
                        </div>
                        {effectiveIntegrationId === integration.id && (
                          <span className="text-xs font-medium text-foreground border border-foreground/30 rounded px-1.5 py-0.5 shrink-0">
                            Selected
                          </span>
                        )}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Footer */}
          <div className="flex items-center justify-between px-6 py-4 border-t border-border">
            <button
              type="button"
              onClick={handleTest}
              disabled={testing || !canTest}
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
                disabled={
                  saveMutation.isPending ||
                  !name ||
                  (type !== "Email" && !effectiveIntegrationId) ||
                  (TARGET_CONFIG[type]?.required && !target.trim())
                }
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
