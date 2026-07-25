import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, Pencil, CalendarClock } from "lucide-react";
import { maintenancesApi, type MaintenanceListItem } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import TableSkeleton from "@/components/TableSkeleton";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import MaintenanceStatusBadge from "../components/MaintenanceStatusBadge";
import { isOneTime, formatMaintenanceDuration } from "../components/maintenanceHelpers";

const NEXT_EVENT_FORMAT: Intl.DateTimeFormatOptions = {
  month: "short", day: "numeric", year: "numeric",
  hour: "numeric", minute: "2-digit",
};

const PAGE_SIZE = 10;

const COLUMNS = ["ID", "Title", "Type", "Duration", "Services", "Next Event", "Status", ""];

const FILTER_OPTIONS = [
  { label: "All", value: "all" },
  { label: "Active", value: "Active" },
  { label: "Scheduled", value: "Scheduled" },
  { label: "Completed", value: "Completed" },
  { label: "Cancelled", value: "Cancelled" },
];

function MaintenancesPage() {
  const navigate = useNavigate();
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const { formatTimestamp } = useFormattedDate();

  function formatNextEvent(m: MaintenanceListItem) {
    if (m.nextEventAt == null) return "—";
    return formatTimestamp(m.nextEventAt, NEXT_EVENT_FORMAT);
  }

  const { data: maintenances = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.MAINTENANCES,
    queryFn: maintenancesApi.list,
  });

  const filtered = maintenances.filter((m) => {
    if (statusFilter === "all") return true;
    return m.displayStatus === statusFilter;
  });

  const totalPages = Math.ceil(filtered.length / PAGE_SIZE);
  const paged = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <PageContainer className="flex flex-col gap-4">
      <PageHeader
        breadcrumbs={[{ label: "Maintenances" }]}
        subheader="Schedule and manage maintenance windows."
        actions={
          <>
            <Select value={statusFilter} onValueChange={(v) => { if (v) { setStatusFilter(v); setPage(1); } }}>
              <SelectTrigger className="w-40">
                <SelectValue>
                  {(v: string) => FILTER_OPTIONS.find((f) => f.value === v)?.label ?? v}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {FILTER_OPTIONS.map((f) => (
                  <SelectItem key={f.value} value={f.value}>{f.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button onClick={() => navigate(ROUTES.MAINTENANCES.NEW)}>
              <Plus size={15} /> New Maintenance
            </Button>
          </>
        }
      />

      <div className="rounded-xl border border-border bg-card overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={COLUMNS} />
        ) : paged.length === 0 ? (
          <Empty className="border-0 py-14">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <CalendarClock />
              </EmptyMedia>
              <EmptyTitle>
                {statusFilter === "all" ? "No maintenances yet" : `No ${statusFilter.toLowerCase()} maintenances`}
              </EmptyTitle>
              <EmptyDescription>
                {statusFilter === "all"
                  ? "Schedule a maintenance window to let users know about planned downtime."
                  : "Try a different status filter, or schedule a new maintenance window."}
              </EmptyDescription>
            </EmptyHeader>
            <Button onClick={() => navigate(ROUTES.MAINTENANCES.NEW)}>
              <Plus size={15} /> New Maintenance
            </Button>
          </Empty>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-16">ID</TableHead>
                <TableHead>Title</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead>Services</TableHead>
                <TableHead>Next Event</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {paged.map((m) => (
                <TableRow key={m.id} className="cursor-pointer" onClick={() => navigate(ROUTES.MAINTENANCES.DETAIL(m.id))}>
                  <TableCell className="text-muted-foreground font-mono text-xs">#{m.id}</TableCell>
                  <TableCell className="font-medium text-foreground">{m.title}</TableCell>
                  <TableCell>
                    <Badge className="bg-blue-500/15 text-blue-600 dark:text-blue-400">
                      {isOneTime(m.rRule) ? "One-Time" : "Recurring"}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{formatMaintenanceDuration(m.durationSeconds)}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {m.isGlobal ? <span className="text-xs text-indigo-600 dark:text-indigo-400 font-medium">All</span> : m.serviceSlugs.length}
                  </TableCell>
                  <TableCell className="text-muted-foreground whitespace-nowrap text-xs">{formatNextEvent(m)}</TableCell>
                  <TableCell>
                    <MaintenanceStatusBadge status={m.displayStatus} />
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={(e) => { e.stopPropagation(); navigate(ROUTES.MAINTENANCES.DETAIL(m.id)); }}
                      className="text-muted-foreground hover:text-foreground"
                    >
                      <Pencil size={14} />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>{(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, filtered.length)} of {filtered.length}</span>
          <Pagination className="mx-0 w-auto justify-end">
            <PaginationContent>
              <PaginationItem>
                <PaginationPrevious
                  href="#"
                  aria-disabled={page <= 1}
                  className={page <= 1 ? "pointer-events-none opacity-50" : undefined}
                  onClick={(e) => { e.preventDefault(); if (page > 1) setPage((p) => p - 1); }}
                />
              </PaginationItem>
              <PaginationItem>
                <PaginationNext
                  href="#"
                  aria-disabled={page >= totalPages}
                  className={page >= totalPages ? "pointer-events-none opacity-50" : undefined}
                  onClick={(e) => { e.preventDefault(); if (page < totalPages) setPage((p) => p + 1); }}
                />
              </PaginationItem>
            </PaginationContent>
          </Pagination>
        </div>
      )}
    </PageContainer>
  );
}

export default MaintenancesPage;
