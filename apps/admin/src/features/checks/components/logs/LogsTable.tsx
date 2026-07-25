import { StatusPill } from "@/components/StatusBadge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import type { CheckDataPoint } from "@/lib/actions/checks";

interface Props {
  logs: CheckDataPoint[];
}

function LogsTable(props: Props) {
  const { logs } = props;
  const { formatTimestamp } = useFormattedDate();

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="text-xs font-semibold">Time</TableHead>
          <TableHead className="text-xs font-semibold">Status</TableHead>
          <TableHead className="text-xs font-semibold">Latency</TableHead>
          <TableHead className="text-xs font-semibold">Region</TableHead>
          <TableHead className="text-xs font-semibold">Message</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {logs.map((log, i) => (
          <TableRow key={`${log.timestamp}-${i}`}>
            <TableCell className="text-xs text-muted-foreground whitespace-nowrap">
              {formatTimestamp(log.timestamp)}
            </TableCell>
            <TableCell>
              <StatusPill status={log.status} dataType={log.dataType} />
            </TableCell>
            <TableCell className="text-sm text-muted-foreground">
              {log.latencyMs != null ? `${Math.round(log.latencyMs)} ms` : "—"}
            </TableCell>
            <TableCell className="text-xs text-muted-foreground">
              {log.workerRegion && log.workerRegion !== "monitor" ? log.workerRegion : "—"}
            </TableCell>
            <TableCell className="text-xs text-muted-foreground">{log.errorMessage ?? ""}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default LogsTable;
