import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { DndContext, closestCenter, PointerSensor, useSensor, useSensors, type DragEndEvent } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy, arrayMove } from "@dnd-kit/sortable";
import { toast } from "sonner";
import { Plus } from "lucide-react";
import { NotificationPreferenceRow } from "@/components/notification-preferences/NotificationPreferenceRow";
import {
  usersApi,
  type UserNotificationPreference,
  type UpsertNotificationPreference,
} from "@/lib/api";
import { integrationsApi, integrationTypesApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";

const PERSONAL_CAPABILITY = "SendsPersonalNotification";

interface Props {
  userId: number;
}

/** A row still being edited before its first save has no server id yet. */
interface DraftRow {
  key: string;
  draft: UpsertNotificationPreference;
}

function toUpsert(p: UserNotificationPreference): UpsertNotificationPreference {
  // A saved account-fallback pref has no instance; a normal one always does. Editing an existing pref
  // reuses this only for display — the fallback row is read-only in the UI.
  return { integrationInstanceId: p.integrationInstanceId ?? "", handle: p.handle };
}

export function NotificationPreferencesEditor({ userId }: Props) {
  const qc = useQueryClient();
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));

  const { data: allIntegrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });

  const { data: types = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_TYPES,
    queryFn: integrationTypesApi.list,
  });

  // Only integration instances whose type can deliver a personal notification are pickable.
  const personalTypes = new Set(
    types.filter((t) => t.capabilities.includes(PERSONAL_CAPABILITY)).map((t) => t.type),
  );
  const personalIntegrations = allIntegrations.filter((i) => personalTypes.has(i.type));

  const { data: preferences = [], isLoading: prefsLoading } = useQuery({
    queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(userId),
    queryFn: () => usersApi.getNotificationPreferences(userId),
  });

  const [newDrafts, setNewDrafts] = useState<DraftRow[]>([]);

  const reorderPrefs = useMutation({
    mutationFn: (orderedIds: number[]) => usersApi.reorderNotificationPreferences(userId, orderedIds),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(userId) }),
  });

  const deletePref = useMutation({
    mutationFn: (id: number) => usersApi.deleteNotificationPreference(userId, id),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(userId) }),
  });

  function handleRemovePref(id: number) {
    toast.promise(deletePref.mutateAsync(id), {
      loading: "Removing preference…",
      success: "Preference removed.",
      error: "Failed to remove preference.",
    });
  }

  function addDraft() {
    setNewDrafts((prev) => [
      ...prev,
      { key: crypto.randomUUID(), draft: { integrationInstanceId: "", handle: "" } },
    ]);
  }

  function updateDraft(key: string, patch: Partial<UpsertNotificationPreference>) {
    setNewDrafts((prev) => prev.map((d) => (d.key === key ? { ...d, draft: { ...d.draft, ...patch } } : d)));
  }

  function removeDraft(key: string) {
    setNewDrafts((prev) => prev.filter((d) => d.key !== key));
  }

  function handleDraftSaved(key: string) {
    // The row is now persisted server-side — drop the local draft, the query invalidation
    // in the row's own mutation brings it back as a real `preferences` entry.
    setNewDrafts((prev) => prev.filter((d) => d.key !== key));
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) return;
    const oldIndex = preferences.findIndex((p) => p.id === active.id);
    const newIndex = preferences.findIndex((p) => p.id === over.id);
    if (oldIndex === -1 || newIndex === -1) return;
    const reordered = arrayMove(preferences, oldIndex, newIndex);
    reorderPrefs.mutate(reordered.map((p) => p.id));
  }

  return (
    <div className="space-y-3">
      {prefsLoading && <p className="text-sm text-muted-foreground">Loading…</p>}

      {preferences.length === 0 && newDrafts.length === 0 && !prefsLoading && (
        <p className="text-sm text-muted-foreground">No preferences configured. Add one below.</p>
      )}

      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
        <SortableContext items={preferences.map((p) => p.id)} strategy={verticalListSortingStrategy}>
          <div className="space-y-3">
            {preferences.map((pref) => (
              <NotificationPreferenceRow
                key={pref.id}
                userId={userId}
                draft={toUpsert(pref)}
                saved={pref}
                personalIntegrations={personalIntegrations}
                onChange={() => {}}
                onSaved={() => {}}
                onRemove={() => handleRemovePref(pref.id)}
                sortableId={pref.id}
              />
            ))}
          </div>
        </SortableContext>
      </DndContext>

      {newDrafts.map((d) => (
        <NotificationPreferenceRow
          key={d.key}
          userId={userId}
          draft={d.draft}
          saved={null}
          personalIntegrations={personalIntegrations}
          onChange={(patch) => updateDraft(d.key, patch)}
          onSaved={() => handleDraftSaved(d.key)}
          onRemove={() => removeDraft(d.key)}
        />
      ))}

      <button
        type="button"
        onClick={addDraft}
        className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
      >
        <Plus size={14} />
        Add preference
      </button>
    </div>
  );
}
