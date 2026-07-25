import { CheckCheck, XCircle } from "lucide-react";
import { Empty, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import type { EscalationDeliveryLog } from "@/lib/actions/alerts";

interface Props {
  logs: EscalationDeliveryLog[];
}

function EscalationLogsTable(props: Props) {
  const { logs } = props;
  const { formatDateTimeOrDash } = useFormattedDate();

  if (logs.length === 0) {
    return (
      <Empty className="border-0 py-6">
        <EmptyHeader>
          <EmptyTitle className="text-muted-foreground font-normal">No escalation activity yet.</EmptyTitle>
        </EmptyHeader>
      </Empty>
    );
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="text-xs font-semibold">Step</TableHead>
          <TableHead className="text-xs font-semibold">User</TableHead>
          <TableHead className="text-xs font-semibold">Channel</TableHead>
          <TableHead className="text-xs font-semibold">Result</TableHead>
          <TableHead className="text-xs font-semibold">When</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {logs.map((log, i) => (
          <TableRow key={i}>
            <TableCell>{log.stepIndex + 1}</TableCell>
            <TableCell className="font-medium">{log.userName}</TableCell>
            <TableCell className="text-muted-foreground">{log.channelType}</TableCell>
            <TableCell>
              {log.succeeded ? (
                <span className="inline-flex items-center gap-1.5 text-green-600 dark:text-green-400">
                  <CheckCheck size={14} /> Sent
                </span>
              ) : (
                <span
                  className="inline-flex items-center gap-1.5 text-destructive"
                  title={log.errorMessage ?? undefined}
                >
                  <XCircle size={14} /> Failed
                </span>
              )}
            </TableCell>
            <TableCell className="text-muted-foreground">{formatDateTimeOrDash(log.attemptedAt)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default EscalationLogsTable;
