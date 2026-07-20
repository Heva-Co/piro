import { useMemo, useState } from "react";
import type { ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Icon } from "@iconify/react";
import { Send, User, Hash, Plug } from "lucide-react";
import { deliveryLogsApi } from "@/lib/actions/delivery-logs";
import type { DeliveryStatus } from "@/lib/actions/delivery-logs";
import { integrationTypesApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { PageHeader } from "@/components/PageHeader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import TableSkeleton from "@/components/TableSkeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationPrevious,
  PaginationNext,
} from "@/components/ui/pagination";

const PAGE_SIZE = 25;
const columns = ["Event", "Destination", "Status", "Detail", "When"];
const STATUS_FILTERS: (DeliveryStatus | "All")[] = ["All", "Delivered", "Failed", "Skipped"];

function statusVariant(status: DeliveryStatus): "default" | "destructive" | "secondary" {
  switch (status) {
    case "Delivered": return "default";
    case "Failed": return "destructive";
    default: return "secondary"; // Skipped
  }
}

// An icon per delivery target kind, so the destination type reads at a glance.
function targetKindIcon(targetKind: string): ReactNode {
  switch (targetKind) {
    case "Personal": return <User size={14} className="text-muted-foreground shrink-0" />;
    case "Channel": return <Hash size={14} className="text-muted-foreground shrink-0" />;
    case "Integration": return <Plug size={14} className="text-muted-foreground shrink-0" />;
    default: return null;
  }
}

function DeliveryLogsPage() {
  const { formatDateTime } = useFormattedDate();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<DeliveryStatus | "All">("All");

  const { data, isLoading } = useQuery({
    queryKey: [...QUERY_KEYS.NOTIFICATION_DELIVERY_LOGS, page, status],
    queryFn: () =>
      deliveryLogsApi.list({
        page,
        pageSize: PAGE_SIZE,
        status: status === "All" ? undefined : status,
      }),
  });

  // Integration type metadata carries each type's iconify logo (e.g. logos:google-meet).
  const { data: types } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_TYPES,
    queryFn: () => integrationTypesApi.list(),
  });
  const iconByType = useMemo(() => {
    const map = new Map<string, string>();
    for (const t of types ?? []) if (t.iconifyIcon) map.set(t.type, t.iconifyIcon);
    return map;
  }, [types]);

  // The integration's real logo when we know its type, else a generic icon by target kind.
  function destinationIcon(targetKind: string, integrationType: string | null): ReactNode {
    const iconify = integrationType ? iconByType.get(integrationType) : undefined;
    if (iconify) return <Icon icon={iconify} className="size-3.5 shrink-0" />;
    return targetKindIcon(targetKind);
  }
  const logs = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        breadcrumbs={[{ label: "Logs" }, { label: "Delivery Logs" }]}
        subheader="Every notification delivery attempt — what fired, where it went, and what happened."
        actions={
          <div className="flex items-center gap-1">
            {STATUS_FILTERS.map((s) => (
              <Button
                key={s}
                size="sm"
                variant={status === s ? "default" : "outline"}
                onClick={() => { setStatus(s); setPage(1); }}
              >
                {s}
              </Button>
            ))}
          </div>
        }
      />

      <div className="rounded-xl border border-border bg-card overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={columns} />
        ) : logs.length === 0 ? (
          <div className="p-8 text-center text-sm text-muted-foreground">No delivery attempts yet.</div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                {columns.map((column) => <TableHead key={column}>{column}</TableHead>)}
              </TableRow>
            </TableHeader>
            <TableBody>
              {logs.map((log) => (
                <TableRow key={log.id}>
                  <TableCell className="px-5 py-3.5">
                    <div className="flex items-center gap-2">
                      <Send size={13} className="text-muted-foreground shrink-0" />
                      <span className="font-mono text-xs text-foreground">{log.eventType}</span>
                    </div>
                  </TableCell>
                  <TableCell className="px-5 py-3.5 text-muted-foreground text-xs">
                    <div className="flex items-center gap-2" title={log.targetKind}>
                      {destinationIcon(log.targetKind, log.integrationType)}
                      <span>{log.targetDescriptor}</span>
                    </div>
                  </TableCell>
                  <TableCell className="px-5 py-3.5">
                    <Badge variant={statusVariant(log.status)}>{log.status}</Badge>
                  </TableCell>
                  <TableCell className="px-5 py-3.5 text-muted-foreground text-xs max-w-xs truncate" title={log.error ?? ""}>
                    {log.error ?? "—"}
                  </TableCell>
                  <TableCell className="px-5 py-3.5 text-muted-foreground text-xs whitespace-nowrap">
                    {formatDateTime(log.attemptedAt)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
            {totalCount > 0 && (
              <TableFooter>
                <TableRow className="hover:bg-transparent">
                  <TableCell colSpan={columns.length} className="px-4 py-3">
                    <div className="flex items-center justify-between text-sm font-normal">
                      <span className="text-muted-foreground">
                        Page {page} of {totalPages} · {totalCount} attempt{totalCount === 1 ? "" : "s"}
                      </span>
                      <Pagination className="mx-0 w-auto">
                        <PaginationContent>
                          <PaginationItem>
                            <PaginationPrevious
                              href="#"
                              onClick={(e) => { e.preventDefault(); if (page > 1) setPage((p) => p - 1); }}
                              className={page <= 1 ? "pointer-events-none opacity-50" : ""}
                            />
                          </PaginationItem>
                          <PaginationItem>
                            <PaginationLink href="#" isActive size="default" className="pointer-events-none px-3">
                              {page} / {totalPages}
                            </PaginationLink>
                          </PaginationItem>
                          <PaginationItem>
                            <PaginationNext
                              href="#"
                              onClick={(e) => { e.preventDefault(); if (page < totalPages) setPage((p) => p + 1); }}
                              className={page >= totalPages ? "pointer-events-none opacity-50" : ""}
                            />
                          </PaginationItem>
                        </PaginationContent>
                      </Pagination>
                    </div>
                  </TableCell>
                </TableRow>
              </TableFooter>
            )}
          </Table>
        )}
      </div>
    </div>
  );
}

export default DeliveryLogsPage;
