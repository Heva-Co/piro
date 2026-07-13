import { useState, useCallback, useEffect } from "react";

const STORAGE_KEY = "piro:timezone-mismatch-dismissed-pair";
/** Fired on the same tab that dismissed, since the native `storage` event only fires on OTHER tabs. */
const DISMISS_EVENT = "piro:timezone-mismatch-dismissed";

function pairKey(profileTimeZone: string, browserTimeZone: string): string {
  return `${profileTimeZone}:${browserTimeZone}`;
}

/**
 * Tracks whether the current profile/browser timezone mismatch banner has been dismissed.
 * Dismissal is keyed to the specific (profile, browser) timezone pair and persisted in
 * localStorage, so it reappears automatically if either timezone changes.
 *
 * Multiple components (banner + header icon) each call this hook independently — dismissing
 * in one must be reflected in the other within the same tab, so we re-read localStorage on a
 * same-tab CustomEvent in addition to the cross-tab native `storage` event.
 */
export function useTimezoneMismatchDismissal(profileTimeZone: string, browserTimeZone: string) {
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
    const key = pairKey(profileTimeZone, browserTimeZone);
    localStorage.setItem(STORAGE_KEY, key);
    setDismissedKey(key);
    window.dispatchEvent(new Event(DISMISS_EVENT));
  }, [profileTimeZone, browserTimeZone]);

  const isDismissed = dismissedKey === pairKey(profileTimeZone, browserTimeZone);

  return { isDismissed, dismiss };
}
