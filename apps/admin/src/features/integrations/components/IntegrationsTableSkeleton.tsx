import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

interface Props {
  rows?: number;
}

/** Loading placeholder mirroring the integrations table shape (Name · Type · Status · action). */
function IntegrationsTableSkeleton(props: Props) {
  const { rows = 3 } = props;

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Type</TableHead>
          <TableHead>Status</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {Array.from({ length: rows }).map((_, i) => (
          <TableRow key={i}>
            <TableCell>
              <Skeleton className="h-4 w-40" />
            </TableCell>
            <TableCell>
              <Skeleton className="h-5 w-24 rounded-full" />
            </TableCell>
            <TableCell>
              <Skeleton className="h-5 w-24 rounded-full" />
            </TableCell>
            <TableCell>
              <Skeleton className="ml-auto h-7 w-24" />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default IntegrationsTableSkeleton;
