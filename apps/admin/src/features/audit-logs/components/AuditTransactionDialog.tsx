import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import AuditActionBadge from "./AuditActionBadge";
import AuditEntryCard from "./AuditEntryCard";
import type { AuditTransaction } from "@/lib/actions/audit-logs";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  transaction: AuditTransaction | null;
}

/**
 * The full detail of one transaction: who did it, when, and a diff per entity it changed.
 *
 * A dialog rather than an expanded row, because a single action can touch several entities and each
 * one's diff is a wide block of monospaced text — that does not fit inside a table row without
 * pushing the columns around.
 */
function AuditTransactionDialog(props: Props) {
  const { open, onOpenChange, transaction } = props;
  const { formatDateTime } = useFormattedDate();

  if (!transaction) return null;

  const entryCount = transaction.entries.length;
  const isAuthEvent = !transaction.entityType;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle className="flex flex-wrap items-center gap-2">
            <AuditActionBadge action={transaction.action} />
            {isAuthEvent ? (
              <span className="text-base">Authentication</span>
            ) : (
              <>
                <span className="font-mono text-base">{transaction.entityType}</span>
                {transaction.entityLabel && (
                  <span className="text-base font-normal text-muted-foreground">
                    {transaction.entityLabel}
                  </span>
                )}
              </>
            )}
          </DialogTitle>
          <DialogDescription>
            {transaction.userEmail || "Unknown user"} · {formatDateTime(transaction.occurredAt)}
            {transaction.ipAddress ? ` · ${transaction.ipAddress}` : ""}
          </DialogDescription>
        </DialogHeader>

        <div className="flex max-h-[65vh] flex-col gap-3 overflow-y-auto">
          {entryCount === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">
              This event recorded no entity changes.
            </p>
          ) : (
            transaction.entries.map((entry) => <AuditEntryCard key={entry.id} entry={entry} />)
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default AuditTransactionDialog;
