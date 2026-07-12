import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { toast } from "sonner";
import { CheckCircle2, GripVertical, ShieldAlert, Trash2 } from "lucide-react";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import {
  usersApi,
  type Integration,
  type PersonalNotificationChannelType,
  type UserNotificationPreference,
  type UpsertNotificationPreference,
} from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

const CHANNEL_INTEGRATION_TYPE: Partial<Record<PersonalNotificationChannelType, string>> = {
  Telegram: "Telegram",
  TwilioSms: "Twilio",
  Ntfy: "Ntfy",
};

const CHANNEL_OPTIONS: { value: PersonalNotificationChannelType; label: string }[] = [
  { value: "Email", label: "Email" },
  { value: "Telegram", label: "Telegram" },
  { value: "TwilioSms", label: "SMS" },
  { value: "Ntfy", label: "Ntfy" },
];

function channelLabel(channel: PersonalNotificationChannelType): string {
  return CHANNEL_OPTIONS.find((c) => c.value === channel)?.label ?? channel;
}

function getHandlePlaceholder(channel: PersonalNotificationChannelType): string {
  switch (channel) {
    case "Telegram": return "Chat ID";
    case "TwilioSms": return "+1234567890";
    case "Email": return "your@email.com";
    case "Ntfy": return "Topic";
    default: return "Handle";
  }
}

interface Props {
  userId: number;
  draft: UpsertNotificationPreference;
  saved: UserNotificationPreference | null;
  allIntegrations: Integration[];
  onChange: (patch: Partial<UpsertNotificationPreference>) => void;
  onSaved: (saved: UserNotificationPreference) => void;
  onRemove: () => void;
  /** Enables drag-and-drop reordering via dnd-kit's useSortable — only meaningful for already-saved rows. */
  sortableId?: number;
}

export function NotificationPreferenceRow(props: Props) {
  const { userId, draft, saved, allIntegrations, onChange, onSaved, onRemove, sortableId } = props;
  const qc = useQueryClient();
  const [codeSent, setCodeSent] = useState(false);
  const [code, setCode] = useState("");

  const sortable = useSortable({ id: sortableId ?? "unsortable", disabled: sortableId === undefined });
  const style = sortableId !== undefined
    ? { transform: CSS.Transform.toString(sortable.transform), transition: sortable.transition }
    : undefined;

  const requiredIntegrationType = CHANNEL_INTEGRATION_TYPE[draft.channel];
  const compatibleIntegrations = requiredIntegrationType
    ? allIntegrations.filter((i) => i.type === requiredIntegrationType)
    : [];

  const canSave = draft.handle.trim().length > 0 && (!requiredIntegrationType || draft.integrationId !== null);

  const savePref = useMutation({
    mutationFn: () => usersApi.createNotificationPreference(userId, draft),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(userId) });
      onSaved(created);
    },
  });

  function handleSave() {
    toast.promise(savePref.mutateAsync(), {
      loading: "Saving preference…",
      success: "Preference saved.",
      error: "Failed to save preference.",
    });
  }

  const sendCode = useMutation({
    mutationFn: () => usersApi.sendNotificationPreferenceCode(userId, saved!.id),
    onSuccess: () => setCodeSent(true),
  });

  function handleSendCode() {
    toast.promise(sendCode.mutateAsync(), {
      loading: "Sending verification code…",
      success: `Code sent via ${channelLabel(draft.channel)}.`,
      error: "Failed to send verification code.",
    });
  }

  const confirmCode = useMutation({
    mutationFn: () => usersApi.confirmNotificationPreferenceCode(userId, saved!.id, code),
    onSuccess: (result) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(userId) });
      onSaved(result);
    },
  });

  function handleConfirmCode() {
    toast.promise(confirmCode.mutateAsync(), {
      loading: "Confirming code…",
      success: "Preference verified.",
      error: "Invalid or expired code.",
    });
  }

  const isEditable = saved === null;

  return (
    <div
      ref={sortableId !== undefined ? sortable.setNodeRef : undefined}
      style={style}
      className={cn("flex flex-col gap-2 rounded-lg border border-border p-3 bg-card", sortable.isDragging && "opacity-50 z-10")}
    >
      <div className="flex items-center gap-2">
        <button
          type="button"
          disabled={sortableId === undefined}
          className="text-muted-foreground hover:text-foreground disabled:opacity-30 leading-none cursor-grab active:cursor-grabbing touch-none"
          {...(sortableId !== undefined ? { ...sortable.attributes, ...sortable.listeners } : {})}
        >
          <GripVertical size={14} />
        </button>

        <Select
          value={draft.channel}
          onValueChange={(v) => v && onChange({ channel: v as PersonalNotificationChannelType, integrationId: null })}
          disabled={!isEditable}
        >
          <SelectTrigger className="w-40">
            <SelectValue>{channelLabel(draft.channel)}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {CHANNEL_OPTIONS.map((c) => (
              <SelectItem key={c.value} value={c.value}>{c.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        {requiredIntegrationType && (
          <Select
            value={draft.integrationId ? String(draft.integrationId) : ""}
            onValueChange={(v) => v && onChange({ integrationId: Number(v) })}
            disabled={!isEditable}
          >
            <SelectTrigger className="w-48">
              <SelectValue placeholder="Select integration">
                {compatibleIntegrations.find((i) => i.id === draft.integrationId)?.name}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {compatibleIntegrations.map((i) => (
                <SelectItem key={i.id} value={String(i.id)}>{i.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}

        <Input
          value={draft.handle}
          onChange={(e) => onChange({ handle: e.target.value })}
          placeholder={getHandlePlaceholder(draft.channel)}
          disabled={!isEditable}
        />

        {isEditable ? (
          <Button size="sm" onClick={handleSave} disabled={!canSave || savePref.isPending}>
            Save
          </Button>
        ) : saved.isVerified ? (
          <span className="flex items-center gap-1 text-xs text-emerald-600 dark:text-emerald-400 shrink-0">
            <CheckCircle2 size={14} /> Verified
          </span>
        ) : (
          <span className="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400 shrink-0">
            <ShieldAlert size={14} /> Unverified
          </span>
        )}

        {!saved?.isAccountFallback && (
          <button type="button" onClick={onRemove} className="text-muted-foreground hover:text-destructive transition-colors">
            <Trash2 size={15} />
          </button>
        )}
      </div>

      {saved && !saved.isVerified && (
        <div className="flex items-center gap-2 pl-6">
          {!codeSent ? (
            <Button size="sm" variant="outline" onClick={handleSendCode} disabled={sendCode.isPending}>
              Send verification code
            </Button>
          ) : (
            <>
              <Input
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="6-digit code"
                className="w-32"
              />
              <Button size="sm" onClick={handleConfirmCode} disabled={!code || confirmCode.isPending}>
                Confirm
              </Button>
            </>
          )}
        </div>
      )}
    </div>
  );
}
