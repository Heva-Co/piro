import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { auditLogsApi } from "@/lib/actions/audit-logs";
import type { AuditAction, AuditTransaction } from "@/lib/actions/audit-logs";
import { QUERY_KEYS } from "@/constants/api";
import { PageHeader } from "@/components/PageHeader";
import { AutoRefreshButton } from "@/components/AutoRefreshButton";
import { Button } from "@/components/ui/button";
import TableSkeleton from "@/components/TableSkeleton";
import AuditTransactionRow from "../components/AuditTransactionRow";
import AuditTransactionDialog from "../components/AuditTransactionDialog";
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

const columns = ["When", "User", "Action", "Entity", "Changes", "IP"];

const ACTION_FILTERS: (AuditAction | "All")[] = [
  "All",
  "Create",
  "Update",
  "Delete",
  "Login",
  "LoginFailed",
  "Logout",
];

function AuditLogsPage() {
  const [page, setPage] = useState(1);
  const [action, setAction] = useState<AuditAction | "All">("All");
  const [selected, setSelected] = useState<AuditTransaction | null>(null);

  const params = {
    page,
    pageSize: PAGE_SIZE,
    action: action === "All" ? undefined : action,
  };

  const { data, isLoading, refetch } = useQuery({
    queryKey: QUERY_KEYS.AUDIT_LOGS(params),
    queryFn: () => auditLogsApi.list(params),
  });

  const transactions = data?.items ?? [];
  // Counts transactions, not entity changes — the backend paginates by transaction.
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        breadcrumbs={[{ label: "Logs" }, { label: "Audit Log" }]}
        subheader="Who changed what, and what the values were before and after."
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <div className="flex flex-wrap items-center gap-1">
              {ACTION_FILTERS.map((value) => (
                <Button
                  key={value}
                  size="sm"
                  variant={action === value ? "default" : "outline"}
                  onClick={() => {
                    setAction(value);
                    setPage(1);
                  }}
                >
                  {value}
                </Button>
              ))}
            </div>

            {/* The trail only grows, so a manual refresh is how you see what happened since. */}
            <AutoRefreshButton onRefetch={refetch} />
          </div>
        }
      />

      <div className="overflow-hidden rounded-xl border border-border bg-card">
        {isLoading ? (
          <TableSkeleton columns={columns} />
        ) : transactions.length === 0 ? (
          <div className="p-8 text-center text-sm text-muted-foreground">
            Nothing recorded yet. Changes made from the admin will appear here.
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                {columns.map((column) => (
                  <TableHead key={column}>{column}</TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {transactions.map((transaction) => (
                <AuditTransactionRow
                  key={transaction.correlationId}
                  transaction={transaction}
                  onSelect={setSelected}
                />
              ))}
            </TableBody>
            {totalCount > 0 && (
              <TableFooter>
                <TableRow className="hover:bg-transparent">
                  <TableCell colSpan={columns.length} className="px-4 py-3">
                    <div className="flex items-center justify-between text-sm font-normal">
                      <span className="text-muted-foreground">
                        Page {page} of {totalPages} · {totalCount} change
                        {totalCount === 1 ? "" : "s"}
                      </span>
                      <Pagination className="mx-0 w-auto">
                        <PaginationContent>
                          <PaginationItem>
                            <PaginationPrevious
                              href="#"
                              onClick={(e) => {
                                e.preventDefault();
                                if (page > 1) setPage((p) => p - 1);
                              }}
                              className={page <= 1 ? "pointer-events-none opacity-50" : ""}
                            />
                          </PaginationItem>
                          <PaginationItem>
                            <PaginationLink
                              href="#"
                              isActive
                              size="default"
                              className="pointer-events-none px-3"
                            >
                              {page} / {totalPages}
                            </PaginationLink>
                          </PaginationItem>
                          <PaginationItem>
                            <PaginationNext
                              href="#"
                              onClick={(e) => {
                                e.preventDefault();
                                if (page < totalPages) setPage((p) => p + 1);
                              }}
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

      {/* Kept mounted so closing animates out; it renders nothing without a selection. */}
      <AuditTransactionDialog
        open={selected !== null}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        transaction={selected}
      />
    </div>
  );
}

export default AuditLogsPage;
