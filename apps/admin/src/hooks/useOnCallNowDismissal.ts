import { useState, useCallback, useEffect } from "react";
import type { OnCallSlot } from "@/lib/api";

const STORAGE_KEY = "piro:oncall-now-dismissed-slot";
/** Fired on the same tab that dismissed, since the native `storage` event only fires on OTHER tabs. */
const DISMISS_EVENT = "piro:oncall-now-dismissed";

function slotKey(slot: OnCallSlot): string {
  return `${slot.scheduleId}:${slot.startsAt}`;
}

/**
 * Tracks whether the current on-call slot's "you're on-call now" banner has been
 * dismissed. Dismissal is keyed to the specific slot (schedule + shift start) and
 * persisted in localStorage, so it survives reloads/logins for the rest of that
 * shift but reappears automatically once a new shift begins.
 *
 * Multiple components (banner + header icon) each call this hook independently —
 * dismissing in one must be reflected in the other within the same tab, so we
 * re-read localStorage on a same-tab CustomEvent in addition to the cross-tab
 * native `storage` event.
 */
export function useOnCallNowDismissal(currentSlot: OnCallSlot | null | undefined) {
  const [dismissedKey, setDismissedKey] = useState(() => localStorage.getItem(STORAGE_KEY));

  useEffect(() => {
    function sync() {
      setDismissedKey(localStorage.getItem(STORAGE_KEY));
    }
    window.addEventListener("storage", sync);
    window.addEventListener(DISMISS_EVENT, sync);
    return () => {
      window.removeEventListener("storage", sync);
      window.removeEventListener(DISMISS_EVENT, sync);
    };
  }, []);

  const dismiss = useCallback(() => {
    if (!currentSlot) return;
    const key = slotKey(currentSlot);
    localStorage.setItem(STORAGE_KEY, key);
    setDismissedKey(key);
    window.dispatchEvent(new Event(DISMISS_EVENT));
  }, [currentSlot]);

  const isDismissed = currentSlot ? dismissedKey === slotKey(currentSlot) : false;

  return { isDismissed, dismiss };
}
