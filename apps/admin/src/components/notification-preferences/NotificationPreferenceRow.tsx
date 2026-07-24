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
  type UserNotificationPreference,
  type UpsertNotificationPreference,
} from "@/lib/api";
import type { Integration } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";

// Handle placeholder by the integration type the chosen instance is (Email is the read-only account fallback).
function handlePlaceholder(integrationType: string | undefined): string {
  switch (integrationType) {
    case "Telegram": return "Chat ID";
    case "Twilio": return "+1234567890";
    case "Ntfy": return "Topic";
    case "Email": return "your@email.com";
    default: return "Handle";
  }
}

interface Props {
  userId: number;
  draft: UpsertNotificationPreference;
  saved: UserNotificationPreference | null;
  /** Integrations whose type supports personal notifications — the pickable instances for a new row. */
  personalIntegrations: Integration[];
  onChange: (patch: Partial<UpsertNotificationPreference>) => void;
  onSaved: (saved: UserNotificationPreference) => void;
  onRemove: () => void;
  /** Enables drag-and-drop reordering via dnd-kit's useSortable — only meaningful for already-saved rows. */
  sortableId?: number;
}

export function NotificationPreferenceRow(props: Props) {
  const { userId, draft, saved, personalIntegrations, onChange, onSaved, onRemove, sortableId } = props;
  const qc = useQueryClient();
  const [codeSent, setCodeSent] = useState(false);
  const [code, setCode] = useState("");

  const sortable = useSortable({ id: sortableId ?? "unsortable", disabled: sortableId === undefined });
  const style = sortableId !== undefined
    ? { transform: CSS.Transform.toString(sortable.transform), transition: sortable.transition }
    : undefined;

  const isEditable = saved === null;

  // For a saved row, the type comes from the DTO's derived integrationId ("Email"/"Telegram"/…). For a
  // new draft, it's the type of the picked integration instance.
  const selectedIntegration = personalIntegrations.find((i) => i.id === draft.integrationInstanceId);
  const integrationType = saved?.integrationId ?? selectedIntegration?.type;
  const displayName = saved?.integrationName
    ?? selectedIntegration?.name
    ?? (saved?.isAccountFallback ? "Account email" : undefined);

  const canSave = draft.handle.trim().length > 0 && Boolean(draft.integrationInstanceId);

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
      success: `Code sent via ${integrationType ?? "channel"}.`,
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

        {isEditable ? (
          <Select
            value={draft.integrationInstanceId ?? ""}
            onValueChange={(v) => v && onChange({ integrationInstanceId: v })}
          >
            <SelectTrigger className="w-56">
              <SelectValue placeholder="Select an integration">{displayName}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              {personalIntegrations.map((i) => (
                <SelectItem key={i.id} value={i.id}>{i.name} ({i.type})</SelectItem>
              ))}
            </SelectContent>
          </Select>
        ) : (
          <span className="w-56 text-sm truncate">
            {displayName} <span className="text-muted-foreground">({integrationType})</span>
          </span>
        )}

        <Input
          value={draft.handle}
          onChange={(e) => onChange({ handle: e.target.value })}
          placeholder={handlePlaceholder(integrationType)}
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
