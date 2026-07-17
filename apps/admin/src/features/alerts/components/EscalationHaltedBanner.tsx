import { BellOff } from "lucide-react";
import { useFormattedDate } from "@/hooks/useFormattedDate";

interface Props {
  /** When escalation was halted (Alert.escalationExhaustedAt) — the last step exhausted its retries. */
  exhaustedAt: string;
}

/**
 * Banner shown on an active alert whose escalation ladder ran out of retries (RFC 0006). It is NOT
 * resolved — the problem is still open, Piro has just stopped paging. Acknowledging clears the
 * terminal state and lets escalation resume, so the CTA points the operator there.
 */
function EscalationHaltedBanner(props: Props) {
  const { exhaustedAt } = props;
  const { formatDateTime } = useFormattedDate();

  return (
    <div className="flex items-start gap-3 rounded-lg border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-700 dark:text-amber-400">
      <BellOff size={16} className="mt-0.5 shrink-0" />
      <div className="space-y-0.5">
        <p className="font-medium">Escalation halted — all steps exhausted their retries.</p>
        <p className="text-xs text-amber-700/80 dark:text-amber-400/80">
          Paging stopped on {formatDateTime(exhaustedAt)}. This alert is still active (not resolved).
          Acknowledge it to take over and resume escalation.
        </p>
      </div>
    </div>
  );
}

export default EscalationHaltedBanner;
