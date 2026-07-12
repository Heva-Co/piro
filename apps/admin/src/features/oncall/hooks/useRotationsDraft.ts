import { useState } from "react";
import type {
  OnCallLayer,
  OnCallOverride,
  CreateLayerDraft,
  UpdateLayerDraft,
  CreateOverrideDraft,
  SaveRotationsRequest,
} from "@/lib/api";

export interface DraftLayer {
  /** Negative for not-yet-saved layers, so they never collide with real ids. */
  id: number;
  name: string;
  recurrenceRule: string;
  firstOccurrenceStartsAt: string;
  firstOccurrenceEndsAt: string;
  userIds: number[];
  users: OnCallLayer["users"];
  isNew: boolean;
}

export interface DraftOverride {
  id: number;
  userId: number;
  userName: string;
  userColor: string;
  replacesUserId: number | null;
  replacesUserName: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  reason: string | null;
  isNew: boolean;
}

let nextTempId = -1;

function toDraftLayer(layer: OnCallLayer): DraftLayer {
  return {
    id: layer.id,
    name: layer.name,
    recurrenceRule: layer.recurrenceRule,
    firstOccurrenceStartsAt: layer.firstOccurrenceStartsAt,
    firstOccurrenceEndsAt: layer.firstOccurrenceEndsAt,
    userIds: layer.users.map((u) => u.userId),
    users: layer.users,
    isNew: false,
  };
}

function toDraftOverride(ov: OnCallOverride): DraftOverride {
  return {
    id: ov.id,
    userId: ov.userId,
    userName: ov.userName,
    userColor: ov.userColor,
    replacesUserId: ov.replacesUserId,
    replacesUserName: ov.replacesUserName,
    startsAtUtc: ov.startsAtUtc,
    endsAtUtc: ov.endsAtUtc,
    reason: ov.reason,
    isNew: false,
  };
}

/**
 * Stages rotation-layer and override edits locally (create/update/delete) so the schedule
 * detail page can show them immediately in the Gantt without hitting the backend, then
 * flushes everything as one atomic batch when the user presses Save.
 */
export function useRotationsDraft(baseLayers: OnCallLayer[], baseOverrides: OnCallOverride[]) {
  const [layers, setLayers] = useState<DraftLayer[]>(() => baseLayers.map(toDraftLayer));
  const [overrides, setOverrides] = useState<DraftOverride[]>(() => baseOverrides.map(toDraftOverride));
  const [deletedLayerIds, setDeletedLayerIds] = useState<Set<number>>(new Set());
  const [deletedOverrideIds, setDeletedOverrideIds] = useState<Set<number>>(new Set());

  const isDirty =
    layers.some((l) => l.isNew) ||
    overrides.some((o) => o.isNew) ||
    deletedLayerIds.size > 0 ||
    deletedOverrideIds.size > 0 ||
    layers.some((l) => {
      const original = baseLayers.find((b) => b.id === l.id);
      if (!original) return false;
      return (
        original.name !== l.name ||
        original.recurrenceRule !== l.recurrenceRule ||
        original.firstOccurrenceStartsAt !== l.firstOccurrenceStartsAt ||
        original.firstOccurrenceEndsAt !== l.firstOccurrenceEndsAt ||
        JSON.stringify(original.users.map((u) => u.userId)) !== JSON.stringify(l.userIds)
      );
    });

  function reset(nextLayers: OnCallLayer[], nextOverrides: OnCallOverride[]) {
    setLayers(nextLayers.map(toDraftLayer));
    setOverrides(nextOverrides.map(toDraftOverride));
    setDeletedLayerIds(new Set());
    setDeletedOverrideIds(new Set());
  }

  function addLayer(data: Omit<CreateLayerDraft, never> & { users: OnCallLayer["users"] }) {
    setLayers((prev) => [
      ...prev,
      {
        id: nextTempId--,
        name: data.name,
        recurrenceRule: data.recurrenceRule,
        firstOccurrenceStartsAt: data.firstOccurrenceStartsAt,
        firstOccurrenceEndsAt: data.firstOccurrenceEndsAt,
        userIds: data.userIds,
        users: data.users,
        isNew: true,
      },
    ]);
  }

  function updateLayer(layerId: number, data: Omit<CreateLayerDraft, never> & { users: OnCallLayer["users"] }) {
    setLayers((prev) =>
      prev.map((l) =>
        l.id === layerId
          ? {
              ...l,
              name: data.name,
              recurrenceRule: data.recurrenceRule,
              firstOccurrenceStartsAt: data.firstOccurrenceStartsAt,
              firstOccurrenceEndsAt: data.firstOccurrenceEndsAt,
              userIds: data.userIds,
              users: data.users,
            }
          : l
      )
    );
  }

  function deleteLayer(layerId: number) {
    setLayers((prev) => prev.filter((l) => l.id !== layerId));
    if (layerId > 0) setDeletedLayerIds((prev) => new Set(prev).add(layerId));
  }

  function addOverride(data: CreateOverrideDraft & { userName: string; userColor: string; replacesUserName: string | null }) {
    setOverrides((prev) => [
      ...prev,
      {
        id: nextTempId--,
        userId: data.userId,
        userName: data.userName,
        userColor: data.userColor,
        replacesUserId: data.replacesUserId ?? null,
        replacesUserName: data.replacesUserName,
        startsAtUtc: data.startsAtUtc,
        endsAtUtc: data.endsAtUtc,
        reason: data.reason ?? null,
        isNew: true,
      },
    ]);
  }

  function deleteOverride(overrideId: number) {
    setOverrides((prev) => prev.filter((o) => o.id !== overrideId));
    if (overrideId > 0) setDeletedOverrideIds((prev) => new Set(prev).add(overrideId));
  }

  function toBatch(): SaveRotationsRequest {
    return {
      layersToCreate: layers
        .filter((l) => l.isNew)
        .map((l) => ({
          name: l.name,
          recurrenceRule: l.recurrenceRule,
          firstOccurrenceStartsAt: l.firstOccurrenceStartsAt,
          firstOccurrenceEndsAt: l.firstOccurrenceEndsAt,
          userIds: l.userIds,
        })),
      layersToUpdate: layers
        .filter((l) => !l.isNew)
        .filter((l) => {
          const original = baseLayers.find((b) => b.id === l.id);
          if (!original) return false;
          return (
            original.name !== l.name ||
            original.recurrenceRule !== l.recurrenceRule ||
            original.firstOccurrenceStartsAt !== l.firstOccurrenceStartsAt ||
            original.firstOccurrenceEndsAt !== l.firstOccurrenceEndsAt ||
            JSON.stringify(original.users.map((u) => u.userId)) !== JSON.stringify(l.userIds)
          );
        })
        .map((l): UpdateLayerDraft => ({
          layerId: l.id,
          name: l.name,
          recurrenceRule: l.recurrenceRule,
          firstOccurrenceStartsAt: l.firstOccurrenceStartsAt,
          firstOccurrenceEndsAt: l.firstOccurrenceEndsAt,
          userIds: l.userIds,
        })),
      layerIdsToDelete: Array.from(deletedLayerIds),
      overridesToCreate: overrides
        .filter((o) => o.isNew)
        .map((o) => ({
          userId: o.userId,
          replacesUserId: o.replacesUserId ?? undefined,
          startsAtUtc: o.startsAtUtc,
          endsAtUtc: o.endsAtUtc,
          reason: o.reason ?? undefined,
        })),
      overrideIdsToDelete: Array.from(deletedOverrideIds),
    };
  }

  return {
    layers,
    overrides,
    isDirty,
    reset,
    addLayer,
    updateLayer,
    deleteLayer,
    addOverride,
    deleteOverride,
    toBatch,
  };
}
