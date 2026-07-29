import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import AuditDiffRow from "./AuditDiffRow";
import { buildDiff, hasChanges } from "../lib/diff";

interface Props {
  oldValues: string | null | undefined;
  newValues: string | null | undefined;
}

/**
 * A GitHub-style unified diff of one entity change: removed values on red `-` lines, new values on
 * green `+` lines, one property per row.
 *
 * Unchanged properties are hidden behind a toggle rather than dropped. A snapshot records the whole
 * entity, so most rows are usually noise — but "what else was true at the time" is a fair question
 * to ask of an audit trail, so the context stays available.
 */
function AuditDiffViewer(props: Props) {
  const { oldValues, newValues } = props;
  const [showUnchanged, setShowUnchanged] = useState(false);

  const lines = useMemo(() => buildDiff(oldValues, newValues), [oldValues, newValues]);
  const changed = useMemo(() => lines.filter((line) => line.kind !== "unchanged"), [lines]);

  const unchangedCount = lines.length - changed.length;
  const visible = showUnchanged ? lines : changed;

  if (lines.length === 0) {
    return (
      <div className="px-4 py-3 text-xs text-muted-foreground">
        No property snapshot was recorded for this change.
      </div>
    );
  }

  if (!hasChanges(lines) && !showUnchanged) {
    return (
      <div className="flex items-center justify-between px-4 py-3 text-xs text-muted-foreground">
        <span>No audited property changed.</span>
        <Button variant="ghost" size="sm" onClick={() => setShowUnchanged(true)}>
          Show {unchangedCount} unchanged
        </Button>
      </div>
    );
  }

  return (
    <div>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse font-mono text-xs">
          <tbody>
            {visible.map((line) => (
              <AuditDiffRow key={line.property} line={line} />
            ))}
          </tbody>
        </table>
      </div>

      {unchangedCount > 0 && (
        <div className="border-t border-border px-4 py-2">
          <Button variant="ghost" size="sm" onClick={() => setShowUnchanged((v) => !v)}>
            {showUnchanged ? "Hide" : "Show"} {unchangedCount} unchanged propert
            {unchangedCount === 1 ? "y" : "ies"}
          </Button>
        </div>
      )}
    </div>
  );
}

export default AuditDiffViewer;
