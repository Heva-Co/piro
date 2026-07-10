import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Plus, Pencil } from "lucide-react";
import { incidentsApi } from "@/lib/actions/incidents";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { formatDuration } from "@/utils/date";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell,
} from "@/components/ui/table";

const STATUS_BADGE: Record<string, string> = {
  INVESTIGATING: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  IDENTIFIED:    "bg-orange-500/15 text-orange-600 dark:text-orange-400",
  MONITORING:    "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  RESOLVED:      "bg-green-500/15 text-green-600 dark:text-green-400",
};

const PAGE_SIZE = 10;

const FILTER_OPTIONS = [
  { label: "Active",        value: "active" },
  { label: "All",           value: "all" },
  { label: "Investigating", value: "investigating" },
  { label: "Identified",    value: "identified" },
  { label: "Monitoring",    value: "monitoring" },
  { label: "Resolved",      value: "resolved" },
];

export default function IncidentsPage() {
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
    <>
      <PageHeader
        breadcrumbs={[{ label: "Incidents" }]}
        subheader="Track and manage service disruptions."
        actions={
          <>
            <Select value={stateFilter} onValueChange={(v) => v && setStateFilter(v)}>
              <SelectTrigger className="w-40">
                <SelectValue />
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
        {isLoading && (
          <div className="py-16 text-center text-sm text-muted-foreground">Loading…</div>
        )}
        {!isLoading && paged.length === 0 && (
          <div className="py-14 text-center text-sm text-muted-foreground">
            No {stateFilter !== "all" ? stateFilter : ""} incidents found.
          </div>
        )}
        {!isLoading && paged.length > 0 && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-16">ID</TableHead>
                <TableHead>Title</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Affects</TableHead>
                <TableHead className="w-12"></TableHead>
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
                        <span className="inline-flex items-center rounded px-1.5 py-0.5 text-xs font-medium bg-yellow-100 text-yellow-700 border border-yellow-200">
                          Private
                        </span>
                      )}
                      {inc.mergedIntoIncidentId && (
                        <span className="inline-flex items-center rounded px-1.5 py-0.5 text-xs font-medium bg-purple-100 text-purple-700 border border-purple-200">
                          Merged →#{inc.mergedIntoIncidentId}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground text-xs">
                    {formatDuration(inc.startDateTime, inc.endDateTime ?? undefined)}
                  </TableCell>
                  <TableCell>
                    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATUS_BADGE[inc.status?.toUpperCase()] ?? "bg-muted text-foreground"}`}>
                      {inc.status}
                    </span>
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">
                    {inc.isGlobal
                      ? <span className="text-xs text-indigo-600 font-medium">All</span>
                      : (inc.services?.length ?? 0)}
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
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
            <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
          </div>
        </div>
      )}
    </>
  );
}
