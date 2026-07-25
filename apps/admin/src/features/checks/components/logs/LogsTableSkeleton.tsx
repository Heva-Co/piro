import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

function LogsTableSkeleton() {
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
        {Array.from({ length: 8 }).map((_, i) => (
          <TableRow key={i}>
            <TableCell><Skeleton className="h-4 w-36" /></TableCell>
            <TableCell><Skeleton className="h-5 w-12 rounded-full" /></TableCell>
            <TableCell><Skeleton className="h-4 w-16" /></TableCell>
            <TableCell><Skeleton className="h-4 w-16" /></TableCell>
            <TableCell><Skeleton className="h-4 w-48" /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default LogsTableSkeleton;
