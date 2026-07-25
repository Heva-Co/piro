import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

function ChecksTableSkeleton() {
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
        {Array.from({ length: 4 }).map((_, i) => (
          <TableRow key={i}>
            <TableCell><Skeleton className="h-6 w-16 rounded-full" /></TableCell>
            <TableCell><Skeleton className="h-4 w-36" /></TableCell>
            <TableCell><Skeleton className="h-4 w-28" /></TableCell>
            <TableCell><Skeleton className="h-5 w-14 rounded-full" /></TableCell>
            <TableCell><Skeleton className="h-4 w-20" /></TableCell>
            <TableCell><Skeleton className="h-4 w-8" /></TableCell>
            <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default ChecksTableSkeleton;
