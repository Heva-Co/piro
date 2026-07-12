import { Siren, X } from "lucide-react";
import { useMyOnCallCurrentStatus } from "@/hooks/useOnCallMe";
import { useOnCallNowDismissal } from "@/hooks/useOnCallNowDismissal";

/**
 * Banner shown while the user is currently on-call on a primary (layer 0) rotation.
 * Dismissal is scoped to the current shift (see useOnCallNowDismissal) — it stays
 * hidden across reloads for the rest of the shift, then reappears on the next one.
 */
export function OnCallNowBanner() {
  const { data: currentSlot } = useMyOnCallCurrentStatus();
  const { isDismissed, dismiss } = useOnCallNowDismissal(currentSlot);

  if (!currentSlot || isDismissed) return null;

  return (
    <div className="flex items-center gap-3 px-4 py-2 text-xs bg-blue-50 dark:bg-blue-950/40 border-b border-blue-200 dark:border-blue-900 text-blue-900 dark:text-blue-200">
      <Siren size={14} className="shrink-0" />
      <span className="flex-1">
        You're on-call now for <strong>{currentSlot.scheduleName}</strong>.
      </span>
      <button
        onClick={dismiss}
        className="p-1 rounded-md hover:bg-blue-100 dark:hover:bg-blue-900/50 transition-colors"
        title="Dismiss"
      >
        <X size={14} />
      </button>
    </div>
  );
}
