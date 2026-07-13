import { Globe, X } from "lucide-react";
import { AnimatePresence, motion } from "motion/react";
import { useNavigate } from "react-router-dom";
import { useTimezone } from "@/hooks/useTimezone";
import { useTimezoneMismatchDismissal } from "@/hooks/useTimezoneMismatchDismissal";
import { ROUTES } from "@/constants/routes";

/**
 * Google-Calendar-style prompt shown when the browser's detected timezone
 * differs from the user's profile timezone — lets them switch the display
 * timezone for this session or go update their profile.
 */
export function TimezoneMismatchBanner() {
  const { mismatch, useBrowserTimeZone, browserTimeZone, profileTimeZone, setUseBrowserTimeZone } =
    useTimezone();
  const { isDismissed, dismiss } = useTimezoneMismatchDismissal(profileTimeZone ?? "", browserTimeZone);
  const navigate = useNavigate();

  const visible = mismatch && !useBrowserTimeZone && !isDismissed;

  return (
    <AnimatePresence initial={false}>
      {visible && (
        <motion.div
          initial={{ height: 0, opacity: 0 }}
          animate={{ height: "auto", opacity: 1 }}
          exit={{ height: 0, opacity: 0 }}
          transition={{ duration: 0.2, ease: "easeInOut" }}
          className="overflow-hidden"
        >
          <div className="flex items-center gap-3 px-4 py-2 text-xs bg-amber-50 dark:bg-amber-950/40 border-b border-amber-200 dark:border-amber-900 text-amber-900 dark:text-amber-200">
            <Globe size={14} className="shrink-0" />
            <span className="flex-1">
              Your profile timezone is <strong>{profileTimeZone}</strong>, but this device is set to{" "}
              <strong>{browserTimeZone}</strong>.
            </span>
            <button
              onClick={() => setUseBrowserTimeZone(true)}
              className="rounded-md px-2 py-1 font-medium hover:bg-amber-100 dark:hover:bg-amber-900/50 transition-colors"
            >
              Use {browserTimeZone} for this session
            </button>
            <button
              onClick={() => navigate(ROUTES.PROFILE)}
              className="rounded-md px-2 py-1 font-medium hover:bg-amber-100 dark:hover:bg-amber-900/50 transition-colors"
            >
              Update profile
            </button>
            <button
              onClick={dismiss}
              className="p-1 rounded-md hover:bg-amber-100 dark:hover:bg-amber-900/50 transition-colors"
              title="Dismiss"
            >
              <X size={14} />
            </button>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
