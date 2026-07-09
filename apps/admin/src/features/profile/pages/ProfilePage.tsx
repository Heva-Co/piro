import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { GripVertical, Plus, Trash2, Send } from "lucide-react";
import { toast } from "react-toastify";
import { AdminLayout } from "@/components/AdminLayout";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { profileApi, usersApi, integrationsApi, channelsApi, type UpsertNotificationPreference, INTEGRATION_CATEGORIES } from "@/lib/api";
import { QUERY_KEYS, CHANNEL_TYPE_LABELS } from "@/constants/api";
import { cn } from "@/lib/utils";
import { Input } from "@/components/ui/input";
import { showLoadingToast, updateToastForError, updateToastForSuccess } from "@/utils/toastify";

const COLOR_PALETTE = [
  "#6366f1", "#8b5cf6", "#ec4899", "#ef4444",
  "#f97316", "#eab308", "#22c55e", "#14b8a6",
  "#3b82f6", "#06b6d4", "#64748b", "#78716c",
];

export default function ProfilePage() {
  const qc = useQueryClient();

  const { data: profile, isLoading } = useQuery({
    queryKey: QUERY_KEYS.MY_PROFILE,
    queryFn: profileApi.get,
  });

  const { data: allIntegrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });
  const integrations = allIntegrations.filter((i) => i.category === INTEGRATION_CATEGORIES.Notification);

  const { data: preferences = [], isLoading: prefsLoading } = useQuery({
    queryKey: profile ? QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(profile.id) : ["skip"],
    queryFn: () => usersApi.getNotificationPreferences(profile!.id),
    enabled: !!profile,
  });

  // ── Profile form ─────────────────────────────────────────────────────────
  const [name, setName] = useState("");
  const [color, setColor] = useState("");
  const [profileDirty, setProfileDirty] = useState(false);

  if (profile && !profileDirty && name === "" && color === "") {
    setName(profile.name);
    setColor(profile.color);
  }

  const updateProfile = useMutation({
    mutationFn: profileApi.update,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MY_PROFILE });
      setProfileDirty(false);
    },
  });

  // ── Preferences form ─────────────────────────────────────────────────────
  const [prefs, setPrefs] = useState<UpsertNotificationPreference[]>([]);
  const [prefsDirty, setPrefsDirty] = useState(false);
  const [testingIdx, setTestingIdx] = useState<number | null>(null);

  if (!prefsDirty && preferences.length > 0 && prefs.length === 0) {
    setPrefs(preferences.map((p) => ({ integrationId: p.integrationId, handle: p.handle, priority: p.priority })));
  }

  const savePrefs = useMutation({
    mutationFn: (data: UpsertNotificationPreference[]) =>
      usersApi.setNotificationPreferences(profile!.id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(profile!.id) });
      setPrefsDirty(false);
    },
  });

  async function testPref(idx: number) {
    const pref = prefs[idx];
    if (!pref.handle) {
      toast.error("Enter a handle before testing.");
      return;
    }
    const toastId = showLoadingToast("Sending Test notification!")
    setTestingIdx(idx);
    try {
      await channelsApi.testPersonal({ integrationId: pref.integrationId, handle: pref.handle });
      updateToastForSuccess(toastId, "Test notification sent!")
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? "Failed to send test notification.";
      updateToastForError(toastId, msg)
    } finally {
      setTestingIdx(null);
    }
  }

  function addPref() {
    if (integrations.length === 0) return;
    // Pick first integration not yet in list, or fallback to first integration
    const pick = integrations.find((i) => !prefs.some((p) => p.integrationId === i.id)) ?? integrations[0];
    const next = [...prefs, { integrationId: pick.id, handle: "", priority: prefs.length + 1 }];
    setPrefs(next);
    setPrefsDirty(true);
  }

  function removePref(idx: number) {
    const next = prefs.filter((_, i) => i !== idx).map((p, i) => ({ ...p, priority: i + 1 }));
    setPrefs(next);
    setPrefsDirty(true);
  }

  function updatePref(idx: number, field: keyof UpsertNotificationPreference, value: string | number) {
    const next = prefs.map((p, i) => (i === idx ? { ...p, [field]: value } : p));
    setPrefs(next);
    setPrefsDirty(true);
  }

  function movePref(from: number, to: number) {
    if (to < 0 || to >= prefs.length) return;
    const next = [...prefs];
    const [item] = next.splice(from, 1);
    next.splice(to, 0, item);
    setPrefs(next.map((p, i) => ({ ...p, priority: i + 1 })));
    setPrefsDirty(true);
  }

  if (isLoading) {
    return (
      <AdminLayout title="Profile">
        <div className="py-12 text-center text-muted-foreground text-sm">Loading…</div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout title="Profile">
      <div className="max-w-2xl space-y-6">

        {/* Basic info card */}
        <div className="rounded-xl border border-border bg-card shadow-sm">
          <div className="px-6 py-4 border-b border-border">
            <h2 className="text-sm font-semibold">Basic information</h2>
            <p className="text-xs text-muted-foreground mt-0.5">Your display name and avatar color.</p>
          </div>
          <div className="px-6 py-5 space-y-5">
            <div className="flex items-center gap-4">
              <div
                className="size-12 rounded-full flex items-center justify-center text-white text-lg font-semibold shrink-0"
                style={{ backgroundColor: color || profile?.color }}
              >
                {name ? name.split(" ").map((n) => n[0]).slice(0, 2).join("").toUpperCase() : "?"}
              </div>
              <div>
                <div className="text-sm font-medium">{name || profile?.name}</div>
                <div className="text-xs text-muted-foreground">{profile?.email}</div>
                {profile?.roles.map((r) => (
                  <span key={r} className="inline-block mt-1 mr-1 rounded px-1.5 py-0.5 text-xs bg-muted text-muted-foreground">{r}</span>
                ))}
              </div>
            </div>

            <div className="space-y-1.5">
              <label className="text-sm font-medium">Display name</label>
              <input
                value={name}
                onChange={(e) => { setName(e.target.value); setProfileDirty(true); }}
                className="w-full rounded-lg border border-input bg-transparent px-3 py-2 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">Avatar color</label>
              <div className="flex flex-wrap gap-2">
                {COLOR_PALETTE.map((c) => (
                  <button
                    key={c}
                    type="button"
                    onClick={() => { setColor(c); setProfileDirty(true); }}
                    className={cn(
                      "size-7 rounded-full border-2 transition-all",
                      color === c ? "border-foreground scale-110" : "border-transparent hover:scale-105"
                    )}
                    style={{ backgroundColor: c }}
                  />
                ))}
              </div>
            </div>

            <div className="flex justify-end pt-1">
              <button
                onClick={() => updateProfile.mutate({ name, color })}
                disabled={!profileDirty || updateProfile.isPending}
                className="rounded-lg bg-primary text-primary-foreground px-4 py-2 text-sm font-medium disabled:opacity-50 hover:bg-primary/90 transition-colors"
              >
                {updateProfile.isPending ? "Saving…" : "Save"}
              </button>
            </div>
          </div>
        </div>

        {/* Notification preferences card */}
        <div className="rounded-xl border border-border bg-card shadow-sm">
          <div className="px-6 py-4 border-b border-border">
            <h2 className="text-sm font-semibold">Notification preferences</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Personal handles used when you're on-call. Tried in order — highest priority first.
            </p>
          </div>
          <div className="px-6 py-5 space-y-3">
            {prefsLoading && <p className="text-sm text-muted-foreground">Loading…</p>}

            {prefs.length === 0 && !prefsLoading && (
              <p className="text-sm text-muted-foreground">No preferences configured. Add one below.</p>
            )}

            {prefs.map((pref, idx) => {
              const integration = integrations.find((i) => i.id === pref.integrationId);
              return (
                <div key={idx} className="flex items-center gap-2">
                  <div className="flex flex-col gap-0.5">
                    <button type="button" onClick={() => movePref(idx, idx - 1)} disabled={idx === 0}
                      className="text-muted-foreground hover:text-foreground disabled:opacity-30 leading-none">
                      <GripVertical size={14} />
                    </button>
                  </div>

                  <Select
                    value={String(pref.integrationId)}
                    onValueChange={(v) => updatePref(idx, "integrationId", Number(v))}
                  >
                    <SelectTrigger className="w-64">
                      <SelectValue>
                        {integration
                          ? `${CHANNEL_TYPE_LABELS[integration.type] ?? integration.type} — ${integration.name}`
                          : "Select integration"}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {integrations.map((i) => (
                        <SelectItem key={i.id} value={String(i.id)}>
                          {CHANNEL_TYPE_LABELS[i.type] ?? i.type} — {i.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>

                  <Input
                    value={pref.handle}
                    onChange={(e) => updatePref(idx, "handle", e.target.value)}
                    placeholder={getHandlePlaceholder(integration?.type)}
                  />

                  <button
                    type="button"
                    title="Send test notification"
                    disabled={testingIdx === idx || !pref.handle}
                    onClick={() => testPref(idx)}
                    className="text-muted-foreground hover:text-foreground disabled:opacity-30 transition-colors"
                  >
                    <Send size={15} className={testingIdx === idx ? "animate-pulse" : ""} />
                  </button>

                  <button type="button" onClick={() => removePref(idx)} className="text-muted-foreground hover:text-destructive transition-colors">
                    <Trash2 size={15} />
                  </button>
                </div>
              );
            })}

            {/* Email fallback row — always fires last, no config needed */}
            <div className="flex items-center gap-2 opacity-50">
              <div className="w-4" />
              <div className="w-64 rounded-lg border border-dashed border-input bg-muted/40 px-3 py-2 text-sm text-muted-foreground select-none">
                Email (fallback)
              </div>
              <Input value={profile?.email ?? ""} disabled className="bg-muted/40" />
              <div className="w-3.75" />
              <div className="w-3.75" />
            </div>

            <button
              type="button"
              onClick={addPref}
              disabled={integrations.length === 0}
              className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors disabled:opacity-40"
            >
              <Plus size={14} />
              Add preference
            </button>

            <div className="flex justify-end pt-2">
              <button
                onClick={() => savePrefs.mutate(prefs)}
                disabled={!prefsDirty || savePrefs.isPending}
                className="rounded-lg bg-primary text-primary-foreground px-4 py-2 text-sm font-medium disabled:opacity-50 hover:bg-primary/90 transition-colors"
              >
                {savePrefs.isPending ? "Saving…" : "Save preferences"}
              </button>
            </div>
          </div>
        </div>

      </div>
    </AdminLayout>
  );
}

function getHandlePlaceholder(type?: string): string {
  switch (type) {
    case "Slack": return "U012AB3CD (Slack member ID)";
    case "Telegram": return "Chat ID";
    case "TwilioSms": return "+1234567890";
    case "Email": return "your@email.com";
    case "Pushover": return "User key";
    default: return "Handle";
  }
}
