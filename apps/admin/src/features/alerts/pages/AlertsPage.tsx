import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Search, ExternalLink, BellRing, BellOff, ListChecks, Siren } from "lucide-react";
import { AutoRefreshButton } from "@/components/AutoRefreshButton";
import { PageHeader } from "@/components/PageHeader";
import { StatusPill } from "@/components/StatusBadge";
import { useAllAlerts } from "@/hooks/useChecks";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { ROUTES } from "@/constants/routes";
import StatItemSkeleton from "../components/StatItemSkeleton";
import StatItem from "../components/StatItem";
import { AlertSourceBadge } from "../components/AlertSourceBadge";
import TableSkeleton from "@/components/TableSkeleton";
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { Switch } from "@/components/ui/switch";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationPrevious,
  PaginationNext,
} from "@/components/ui/pagination";
import { InputGroup, InputGroupAddon, InputGroupInput } from "@/components/ui/input-group";

const PAGE_SIZE = 15;

const columns = [
  "Impact",
  "Check",
  "Service",
  "Message",
  "Occurrences",
  "Fired At",
  "Resolved At"
]

export default function AlertsPage() {
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [includeResolved, setIncludeResolved] = useState(false);
  const { formatDateTime } = useFormattedDate();

  function formatDate(value?: string) {
    if (!value) return "—";
    return formatDateTime(value);
  }

  const { data, isLoading, refetch } = useAllAlerts({
    page,
    pageSize: PAGE_SIZE,
    activeOnly: !includeResolved,
  });
  const alerts = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const allTimeTotalCount = data?.allTimeTotalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const filtered = alerts.filter((a) => {
    const q = search.toLowerCase();
    return (
      (a.checkName ?? "").toLowerCase().includes(q) ||
      (a.serviceName ?? "").toLowerCase().includes(q) ||
      (a.message ?? "").toLowerCase().includes(q)
    );
  });

  // Active/linked counts reflect only the currently loaded page — Alert history can span
  // thousands of rows, so these are not meant as instance-wide totals.
  const activeOnPage = alerts.filter((a) => !a.resolvedAt).length;
  const linkedOnPage = alerts.filter((a) => !!a.incidentId).length;

  return (
    <div className="flex flex-col ">
      <PageHeader
        breadcrumbs={[{ label: "Alerts" }]}
        subheader="Alert history across every check — independent of whether they escalated to an incident."
      />

      {/* Stats */}
      <div className="rounded-xl border bg-card divide-x flex">
        {isLoading ? (
          <>
            <StatItemSkeleton />
            <StatItemSkeleton />
            <StatItemSkeleton />
          </>
        ) : (
          <>
            <StatItem icon={<ListChecks size={20} />} label="Total" value={allTimeTotalCount} />
            <StatItem icon={<BellRing size={20} />} label="Active (this page)" value={activeOnPage} color="text-red-600" />
            <StatItem icon={<Siren size={20} />} label="Linked to incident (this page)" value={linkedOnPage} color="text-amber-600" />
          </>
        )}
      </div>

      {/* Table card */}
      <div className="rounded-xl border bg-card overflow-hidden mt-4">
        {/* Search + refresh */}
        <div className="px-4 py-3 border-b flex items-center gap-3">

          <InputGroup className="max-w-xs">
            <InputGroupInput
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search checks, services, messages..." />
            <InputGroupAddon>
              <Search />
            </InputGroupAddon>
          </InputGroup>

          <label className="ml-auto flex items-center gap-2 text-sm text-muted-foreground shrink-0">
            <Switch
              checked={includeResolved}
              onCheckedChange={(checked) => {
                setIncludeResolved(checked);
                setPage(1);
              }}
            />
            Include resolved
          </label>
          <AutoRefreshButton onRefetch={refetch} />
        </div>

        {isLoading ? (
          <TableSkeleton {...{ columns }} />
        ) : filtered.length === 0 ? (
          <div className="px-5 py-8 text-sm text-muted-foreground text-center">
            {search ? "No alerts match your search." : "No alerts recorded yet."}
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                {columns.map((column) => <TableHead key={column}>{column}</TableHead>)}
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((alert) => (
                <TableRow
                  key={alert.id}
                  className="hover:bg-muted/30 transition-colors cursor-pointer"
                  onClick={() => navigate(ROUTES.ALERTS.DETAIL(alert.id))}
                >
                  <TableCell className="px-5 py-3">
                    <StatusPill status={alert.impactAtFireTime} />
                  </TableCell>
                  <TableCell className="px-5 py-3 font-semibold">
                    <div className="flex items-center gap-2">
                      {alert.checkSlug && alert.serviceSlug ? (
                        <Link
                          to={ROUTES.CHECKS.DETAIL(alert.serviceSlug, alert.checkSlug)}
                          onClick={(e) => e.stopPropagation()}
                          className="hover:underline"
                        >
                          {alert.checkName}
                        </Link>
                      ) : (
                        <AlertSourceBadge source={alert.source} sourceLabel={alert.sourceLabel} sourceIconifyIcon={alert.sourceIconifyIcon} />
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="px-5 py-3 text-muted-foreground">
                    <div className="flex items-center gap-1.5">
                      {alert.hasEscalationPolicy ? (
                        <Tooltip >
                          <TooltipTrigger render={<BellRing size={14} className="text-foreground shrink-0" />} />
                          <TooltipContent>
                            Escalation policy assigned — alerts notify on-call
                          </TooltipContent>
                        </Tooltip>
                      ) : (
                        <Tooltip >
                          <TooltipTrigger render={<BellOff size={14} className="text-foreground shrink-0" />} />
                          <TooltipContent>
                            No escalation policy assigned
                          </TooltipContent>
                        </Tooltip>
                      )}
                      {alert.serviceSlug ? (
                        <Link
                          to={ROUTES.SERVICES.DETAIL(alert.serviceSlug)}
                          onClick={(e) => e.stopPropagation()}
                          className="hover:underline"
                        >
                          {alert.serviceName}
                        </Link>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="px-5 py-3 text-muted-foreground max-w-xs truncate" title={alert.message ?? undefined}>
                    {alert.message ?? "—"}
                  </TableCell>
                  <TableCell className="px-5 py-3">{alert.occurrenceCount}</TableCell>
                  <TableCell className="px-5 py-3 text-muted-foreground">{formatDate(alert.firedAt)}</TableCell>
                  <TableCell className={`px-5 py-3 ${alert.resolvedAt ? "text-muted-foreground" : "text-red-600 font-medium"}`}>
                    {alert.resolvedAt ? formatDate(alert.resolvedAt) : "Active"}
                  </TableCell>
                  <TableCell className="px-5 py-3">
                    {alert.incidentId != null && (
                      <div className="flex items-center justify-end">
                        <Link
                          to={ROUTES.INCIDENTS.DETAIL(alert.incidentId)}
                          title="View linked incident"
                          onClick={(e) => e.stopPropagation()}
                          className="text-muted-foreground hover:text-foreground transition-colors"
                        >
                          <ExternalLink size={16} />
                        </Link>
                      </div>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
            {!isLoading && totalCount > 0 && (
              <TableFooter>
                <TableRow className="hover:bg-transparent">
                  <TableCell colSpan={columns.length + 1} className="px-4 py-3">
                    <div className="flex items-center justify-between text-sm font-normal">
                      <span className="text-muted-foreground">
                        Page {page} of {totalPages} · {totalCount} alert{totalCount === 1 ? "" : "s"}
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
                            <PaginationLink href="#" isActive size="default" className="pointer-events-none px-3">
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
    </div>
  );
}
