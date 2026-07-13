
import StatusPill from "@/components/StatusBadge";
import TableSkeleton from "@/components/TableSkeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { Service } from "@/lib/actions/services";

const COLUMNS = ["Name", "Slug", "Status"];

interface Props {
  services: Service[];
  isLoading: boolean;
  isError: boolean;
}

function ServicesTable(props: Props) {
  const { services, isLoading, isError } = props;

  return (
    <div className="flex-1 lg:w-2/3 bg-card rounded-lg border border-border shadow-sm overflow-hidden">
      <div className="px-5 py-4 border-b border-border">
        <h2 className="font-semibold text-foreground">Services</h2>
      </div>
      {isError ? (
        <div className="p-6 text-sm text-destructive">Failed to load services.</div>
      ) : isLoading ? (
        <TableSkeleton columns={COLUMNS} />
      ) : services.length === 0 ? (
        <div className="p-6 text-sm text-muted-foreground">No services found.</div>
      ) : (
        <Table className="min-w-full text-sm">
          <TableHeader>
            <TableRow>
              {COLUMNS.map((column) => (
                <TableHead key={column}>{column}</TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody className="divide-y">
            {services.map((service) => (
              <TableRow key={service.slug}>
                <TableCell className="font-medium text-foreground">{service.name}</TableCell>
                <TableCell className="text-muted-foreground">{service.slug}</TableCell>
                <TableCell>
                  <StatusPill status={service.currentStatus} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}

export default ServicesTable;
