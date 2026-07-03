import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Plus, Pencil, ChevronDown } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { incidentsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { formatDuration } from "@/utils/date";

const STATE_BADGE: Record<string, string> = {
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
    <AdminLayout title="Incidents">
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between">
          {/* Filter dropdown */}
          <div className="relative">
            <select
              value={stateFilter}
              onChange={(e) => setStateFilter(e.target.value)}
              className="appearance-none rounded-lg border border-border bg-background pl-3 pr-8 py-2 text-sm font-medium text-foreground focus:outline-none focus:ring-2 focus:ring-ring cursor-pointer"
            >
              {FILTER_OPTIONS.map(f => (
                <option key={f.value} value={f.value}>{f.label}</option>
              ))}
            </select>
            <ChevronDown size={14} className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
          </div>

          <button
            onClick={() => navigate(ROUTES.INCIDENTS.NEW)}
            className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90"
          >
            <Plus size={15} /> New Incident
          </button>
        </div>

        <div className="rounded-xl border border-border bg-card overflow-hidden">
          <table className="min-w-full text-sm">
            <thead className="border-b border-border bg-muted/50">
              <tr>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground w-16">ID</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Title</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Duration</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Status</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Affects</th>
                <th className="px-5 py-3 w-12"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {isLoading && (
                <tr><td colSpan={6} className="px-5 py-10 text-center text-sm text-muted-foreground">Loading…</td></tr>
              )}
              {!isLoading && paged.length === 0 && (
                <tr><td colSpan={6} className="px-5 py-10 text-center text-sm text-muted-foreground">
                  No {stateFilter !== "all" ? stateFilter : ""} incidents found.
                </td></tr>
              )}
              {paged.map((inc) => (
                <tr key={inc.id} className="hover:bg-muted/50">
                  <td className="px-5 py-3.5 text-muted-foreground font-mono text-xs">#{inc.id}</td>
                  <td className="px-5 py-3.5 font-medium text-foreground">{inc.title}</td>
                  <td className="px-5 py-3.5 text-muted-foreground text-xs">
                    {formatDuration(inc.startDateTime, inc.endDateTime)}
                  </td>
                  <td className="px-5 py-3.5">
                    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATE_BADGE[inc.state?.toUpperCase()] ?? "bg-muted text-foreground"}`}>
                      {inc.state}
                    </span>
                  </td>
                  <td className="px-5 py-3.5 text-muted-foreground text-sm">
                    {inc.isGlobal
                      ? <span className="text-xs text-indigo-600 font-medium">All</span>
                      : (inc.services?.length ?? 0)}
                  </td>
                  <td className="px-5 py-3.5">
                    <button
                      onClick={() => navigate(ROUTES.INCIDENTS.DETAIL(inc.id))}
                      className="rounded-md p-1.5 text-muted-foreground hover:text-gray-600 hover:bg-muted transition-colors"
                    >
                      <Pencil size={14} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {totalPages > 1 && (
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>{(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, incidents.length)} of {incidents.length}</span>
            <div className="flex gap-2">
              <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}
                className="rounded-lg border border-border bg-background px-3 py-1.5 text-sm hover:bg-muted disabled:opacity-40">Previous</button>
              <button disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}
                className="rounded-lg border border-border bg-background px-3 py-1.5 text-sm hover:bg-muted disabled:opacity-40">Next</button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
