import { TableCell, TableRow } from "@/components/ui/table";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import AuditActionBadge from "./AuditActionBadge";
import type { AuditTransaction } from "@/lib/actions/audit-logs";

interface Props {
  transaction: AuditTransaction;
  onSelect: (transaction: AuditTransaction) => void;
}

/**
 * One row per transaction — one thing a person did. Editing a service and its tags is a single row
 * here, not four, which is what the backend's correlation id exists for. Clicking opens the diffs.
 */
function AuditTransactionRow(props: Props) {
  const { transaction, onSelect } = props;
  const { formatDateTime } = useFormattedDate();

  const entryCount = transaction.entries.length;
  // Authentication events carry no entity, so the type column would otherwise read as empty.
  const isAuthEvent = !transaction.entityType;

  return (
    // The whole row is the target: an audit entry is read by drilling into it, so restricting the
    // click to one cell would be a needlessly small target.
    <TableRow
      className="cursor-pointer select-none"
      onClick={() => onSelect(transaction)}
      tabIndex={0}
      onKeyDown={(event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          onSelect(transaction);
        }
      }}
    >
      <TableCell className="px-5 py-3.5 text-xs whitespace-nowrap text-muted-foreground">
        {formatDateTime(transaction.occurredAt)}
      </TableCell>
      <TableCell className="px-5 py-3.5 text-xs">
        <span className="text-foreground">{transaction.userEmail || "—"}</span>
      </TableCell>
      <TableCell className="px-5 py-3.5">
        <AuditActionBadge action={transaction.action} />
      </TableCell>
      <TableCell className="px-5 py-3.5 text-xs">
        {isAuthEvent ? (
          <span className="text-muted-foreground">Authentication</span>
        ) : (
          <div className="flex items-center gap-2">
            <span className="font-mono text-foreground">{transaction.entityType}</span>
            {transaction.entityLabel && (
              <span className="truncate text-muted-foreground">{transaction.entityLabel}</span>
            )}
          </div>
        )}
      </TableCell>
      <TableCell className="px-5 py-3.5 text-xs whitespace-nowrap text-muted-foreground">
        {entryCount === 1 ? "1 change" : `${entryCount} changes`}
      </TableCell>
      <TableCell className="px-5 py-3.5 font-mono text-[11px] whitespace-nowrap text-muted-foreground">
        {transaction.ipAddress ?? "—"}
      </TableCell>
    </TableRow>
  );
}

export default AuditTransactionRow;
