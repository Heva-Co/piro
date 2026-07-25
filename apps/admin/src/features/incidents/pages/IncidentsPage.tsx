import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Plus, Pencil } from "lucide-react";
import { incidentsApi } from "@/lib/actions/incidents";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { formatDuration } from "@/utils/date";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import TableSkeleton from "@/components/TableSkeleton";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import IncidentStatusBadge from "../components/IncidentStatusBadge";

const PAGE_SIZE = 10;

const COLUMNS = ["ID", "Title", "Duration", "Status", "Affects", ""];

const FILTER_OPTIONS = [
  { label: "Active", value: "active" },
  { label: "All", value: "all" },
  { label: "Investigating", value: "investigating" },
  { label: "Identified", value: "identified" },
  { label: "Monitoring", value: "monitoring" },
  { label: "Resolved", value: "resolved" },
];

function IncidentsPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const stateFilter = searchParams.get("filter") ?? "active";
  const [page, setPage] = useState(1);

  function setStateFilter(value: string) {
    setSearchParams(value === "active" ? {} : { filter: value });
    setPage(1);
  }

  const { data: incidents = [], isLoading } = useQuery({
    queryKey: [...QUERY_KEYS.INCIDENTS, stateFilter],
    queryFn: () => incidentsApi.list(stateFilter),
  });

  const totalPages = Math.ceil(incidents.length / PAGE_SIZE);
  const paged = incidents.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[{ label: "Incidents" }]}
        subheader="Track and manage service disruptions."
        actions={
          <>
            <Select value={stateFilter} onValueChange={(v) => v && setStateFilter(v)}>
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
            <Button onClick={() => navigate(ROUTES.INCIDENTS.NEW)}>
              <Plus size={15} /> New Incident
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
              <EmptyTitle className="text-muted-foreground font-normal">
                No {stateFilter !== "all" ? stateFilter : ""} incidents found.
              </EmptyTitle>
            </EmptyHeader>
          </Empty>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-16">ID</TableHead>
                <TableHead>Title</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Affects</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {paged.map((inc) => (
                <TableRow key={inc.id} className="cursor-pointer" onClick={() => navigate(ROUTES.INCIDENTS.DETAIL(inc.id))}>
                  <TableCell className="text-muted-foreground font-mono text-xs">#{inc.id}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-foreground">{inc.title}</span>
                      {inc.visibility !== "Public" && (
                        <Badge variant="outline" className="border-yellow-500/30 bg-yellow-500/10 text-yellow-700 dark:text-yellow-500">
                          Private
                        </Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground text-xs">
                    {formatDuration(inc.startDateTime, inc.endDateTime ?? undefined)}
                  </TableCell>
                  <TableCell>
                    <IncidentStatusBadge status={inc.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">
                    {inc.services?.length ?? 0}
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={(e) => { e.stopPropagation(); navigate(ROUTES.INCIDENTS.DETAIL(inc.id)); }}
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
        <div className="flex items-center justify-between text-sm text-muted-foreground mt-4">
          <span>{(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, incidents.length)} of {incidents.length}</span>
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

export default IncidentsPage;
