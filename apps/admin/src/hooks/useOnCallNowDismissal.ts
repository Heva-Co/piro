import { useState, useCallback } from "react";
import type { OnCallSlot } from "@/lib/api";

const STORAGE_KEY = "piro:oncall-now-dismissed-slot";

function slotKey(slot: OnCallSlot): string {
  return `${slot.scheduleId}:${slot.startsAt}`;
}

/**
 * Tracks whether the current on-call slot's "you're on-call now" banner has been
 * dismissed. Dismissal is keyed to the specific slot (schedule + shift start) and
 * persisted in localStorage, so it survives reloads/logins for the rest of that
 * shift but reappears automatically once a new shift begins.
 */
export function useOnCallNowDismissal(currentSlot: OnCallSlot | null | undefined) {
  const [dismissedKey, setDismissedKey] = useState(() => localStorage.getItem(STORAGE_KEY));

  const dismiss = useCallback(() => {
    if (!currentSlot) return;
    const key = slotKey(currentSlot);
    localStorage.setItem(STORAGE_KEY, key);
    setDismissedKey(key);
  }, [currentSlot]);

  const isDismissed = currentSlot ? dismissedKey === slotKey(currentSlot) : false;

  return { isDismissed, dismiss };
}
