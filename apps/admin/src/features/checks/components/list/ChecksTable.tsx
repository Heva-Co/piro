import { FileText, Settings } from "lucide-react";
import { StatusPill } from "@/components/StatusBadge";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { CheckSummary } from "@/lib/actions/checks";

interface Props {
  checks: CheckSummary[] | undefined;
  typeLabel: (type: string) => string;
  onViewLogs: (check: CheckSummary) => void;
  onConfigure: (check: CheckSummary) => void;
  onNavigateService: (check: CheckSummary) => void;
}

function ChecksTable(props: Props) {
  const { checks, typeLabel, onViewLogs, onConfigure, onNavigateService } = props;

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="text-xs font-semibold">Status</TableHead>
          <TableHead className="text-xs font-semibold">Check</TableHead>
          <TableHead className="text-xs font-semibold">Service</TableHead>
          <TableHead className="text-xs font-semibold">Type</TableHead>
          <TableHead className="text-xs font-semibold">Cron</TableHead>
          <TableHead className="text-xs font-semibold">Active</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {(checks || []).map((check) => (
          <TableRow key={`${check.serviceSlug}-${check.slug}`}>
            <TableCell>
              <StatusPill status={check.currentStatus} />
            </TableCell>
            <TableCell className="font-semibold">{check.name}</TableCell>
            <TableCell>
              <button
                onClick={() => onNavigateService(check)}
                className="text-muted-foreground hover:text-foreground hover:underline transition-colors"
              >
                {check.serviceName}
              </button>
            </TableCell>
            <TableCell>
              <Badge variant="outline">{typeLabel(check.type)}</Badge>
            </TableCell>
            <TableCell className="font-mono text-xs text-muted-foreground">{check.cron}</TableCell>
            <TableCell className={`text-sm font-medium ${check.isActive ? "text-green-600" : "text-muted-foreground"}`}>
              {check.isActive ? "Yes" : "No"}
            </TableCell>
            <TableCell>
              <div className="flex items-center justify-end gap-2">
                <button
                  onClick={() => onViewLogs(check)}
                  title="View logs"
                  className="text-muted-foreground hover:text-foreground transition-colors"
                >
                  <FileText size={16} />
                </button>
                <button
                  onClick={() => onConfigure(check)}
                  title="Configure"
                  className="text-muted-foreground hover:text-foreground transition-colors"
                >
                  <Settings size={16} />
                </button>
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default ChecksTable;
