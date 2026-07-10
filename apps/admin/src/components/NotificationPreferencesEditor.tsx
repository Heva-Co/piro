import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { GripVertical, Plus, Trash2, Send } from "lucide-react";
import { toast } from "react-toastify";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import {
  usersApi,
  integrationsApi,
  channelsApi,
  type UpsertNotificationPreference,
  INTEGRATION_CATEGORIES,
} from "@/lib/api";
import { QUERY_KEYS, CHANNEL_TYPE_LABELS } from "@/constants/api";
import { showLoadingToast, updateToastForError, updateToastForSuccess } from "@/utils/toastify";

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

interface Props {
  userId: number;
  /** Shown as a disabled fallback row at the bottom. Typically the user's own email. */
  fallbackEmail?: string;
}

export function NotificationPreferencesEditor({ userId, fallbackEmail }: Props) {
  const qc = useQueryClient();

  const { data: allIntegrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });
  const integrations = allIntegrations.filter((i) => i.category === INTEGRATION_CATEGORIES.Notification);

  const { data: preferences = [], isLoading: prefsLoading } = useQuery({
    queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(userId),
    queryFn: () => usersApi.getNotificationPreferences(userId),
  });

  const [prefs, setPrefs] = useState<UpsertNotificationPreference[]>([]);
  const [prefsDirty, setPrefsDirty] = useState(false);
  const [testingIdx, setTestingIdx] = useState<number | null>(null);

  if (!prefsDirty && preferences.length > 0 && prefs.length === 0) {
    setPrefs(preferences.map((p) => ({ integrationId: p.integrationId, handle: p.handle, priority: p.priority })));
  }

  const savePrefs = useMutation({
    mutationFn: (data: UpsertNotificationPreference[]) =>
      usersApi.setNotificationPreferences(userId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(userId) });
      setPrefsDirty(false);
    },
  });

  async function testPref(idx: number) {
    const pref = prefs[idx];
    if (!pref.handle) {
      toast.error("Enter a handle before testing.");
      return;
    }
    const toastId = showLoadingToast("Sending Test notification!");
    setTestingIdx(idx);
    try {
      await channelsApi.testPersonal({ integrationId: pref.integrationId, handle: pref.handle });
      updateToastForSuccess(toastId, "Test notification sent!");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? "Failed to send test notification.";
      updateToastForError(toastId, msg);
    } finally {
      setTestingIdx(null);
    }
  }

  function addPref() {
    if (integrations.length === 0) return;
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

  return (
    <div className="space-y-3">
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
              onValueChange={(v) => v && updatePref(idx, "integrationId", Number(v))}
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

      {fallbackEmail && (
        <div className="flex items-center gap-2 opacity-50">
          <div className="w-4" />
          <div className="w-64 rounded-lg border border-dashed border-input bg-muted/40 px-3 py-2 text-sm text-muted-foreground select-none">
            Email (fallback)
          </div>
          <Input value={fallbackEmail} disabled className="bg-muted/40" />
          <div className="w-3.75" />
          <div className="w-3.75" />
        </div>
      )}

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
        <Button onClick={() => savePrefs.mutate(prefs)} disabled={!prefsDirty || savePrefs.isPending}>
          {savePrefs.isPending ? "Saving…" : "Save preferences"}
        </Button>
      </div>
    </div>
  );
}
