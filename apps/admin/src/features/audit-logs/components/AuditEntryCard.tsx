import AuditActionBadge from "./AuditActionBadge";
import AuditDiffViewer from "./AuditDiffViewer";
import type { AuditEntry } from "@/lib/actions/audit-logs";

interface Props {
  entry: AuditEntry;
}

/** One entity change inside a transaction: what it was, and the diff of what changed. */
function AuditEntryCard(props: Props) {
  const { entry } = props;

  return (
    <div className="overflow-hidden rounded-lg border border-border bg-background">
      <div className="flex flex-wrap items-center gap-2 border-b border-border bg-muted/40 px-4 py-2">
        <AuditActionBadge action={entry.action} />
        <span className="font-mono text-xs font-medium text-foreground">{entry.entityType}</span>
        {entry.entityLabel && (
          <span className="truncate text-xs text-muted-foreground">{entry.entityLabel}</span>
        )}
        <span className="ml-auto font-mono text-[11px] text-muted-foreground">#{entry.entityId}</span>
      </div>

      <AuditDiffViewer oldValues={entry.oldValues} newValues={entry.newValues} />
    </div>
  );
}

export default AuditEntryCard;
