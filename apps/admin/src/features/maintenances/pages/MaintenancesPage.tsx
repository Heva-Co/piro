import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, Pencil, ChevronDown } from "lucide-react";
import { maintenancesApi, type Maintenance, type MaintenanceDisplayStatus } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const STATUS_BADGE: Record<MaintenanceDisplayStatus, string> = {
  Active:    "bg-green-500/15 text-green-600 dark:text-green-400",
  Cancelled: "bg-muted text-muted-foreground",
  Scheduled: "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  Completed: "bg-indigo-100 text-indigo-700",
};

const PAGE_SIZE = 10;

function formatDuration(durationSeconds: number) {
  const diff = Math.round(durationSeconds / 60);
  if (diff < 60) return `${diff}m`;
  const h = Math.floor(diff / 60);
  const m = diff % 60;
  return m > 0 ? `${h}h ${m}m` : `${h}h`;
}

function isOneTime(rRule: string) {
  return rRule.includes("COUNT=1");
}

function formatNextEvent(m: Maintenance) {
  const next = m.upcomingEvents[0];
  if (!next) return "—";
  return new Date(next.startDateTime * 1000).toLocaleString("en-US", {
    month: "short", day: "numeric", year: "numeric",
    hour: "numeric", minute: "2-digit",
  });
}

const FILTER_OPTIONS: { label: string; value: "all" | MaintenanceDisplayStatus }[] = [
  { label: "All",       value: "all" },
  { label: "Active",    value: "Active" },
  { label: "Scheduled", value: "Scheduled" },
  { label: "Completed", value: "Completed" },
  { label: "Cancelled", value: "Cancelled" },
];

export default function MaintenancesPage() {
  const navigate = useNavigate();
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);

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
    <>
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between">
          {/* Filter dropdown */}
          <div className="relative">
            <select
              value={statusFilter}
              onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
              className="appearance-none rounded-lg border border-border bg-background pl-3 pr-8 py-2 text-sm font-medium text-foreground focus:outline-none focus:ring-2 focus:ring-gray-400 cursor-pointer"
            >
              {FILTER_OPTIONS.map(f => (
                <option key={f.value} value={f.value}>{f.label}</option>
              ))}
            </select>
            <ChevronDown size={14} className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-500" />
          </div>

          <button
            onClick={() => navigate(ROUTES.MAINTENANCES.NEW)}
            className="flex items-center gap-2 rounded-lg bg-foreground px-4 py-2 text-sm font-medium text-background hover:opacity-90"
          >
            <Plus size={15} /> New Maintenance
          </button>
        </div>

        <div className="rounded-xl border border-border bg-card overflow-hidden">
          <table className="min-w-full text-sm">
            <thead className="border-b border-border bg-muted/30">
              <tr>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide w-16">ID</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Title</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Type</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Duration</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Services</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Next Event</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-5 py-3 w-12"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading && (
                <tr>
                  <td colSpan={8} className="px-5 py-10 text-center text-sm text-gray-400">Loading…</td>
                </tr>
              )}
              {!isLoading && paged.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-5 py-10 text-center text-sm text-gray-400">No maintenances found.</td>
                </tr>
              )}
              {paged.map((m) => (
                <tr key={m.id} className="hover:bg-muted">
                  <td className="px-5 py-3.5 text-gray-400 font-mono text-xs">#{m.id}</td>
                  <td className="px-5 py-3.5 font-medium text-gray-900">{m.title}</td>
                  <td className="px-5 py-3.5">
                    <span className="inline-flex items-center rounded-full bg-blue-500/15 text-blue-600 dark:text-blue-400 px-2.5 py-0.5 text-xs font-medium">
                      {isOneTime(m.rRule) ? "One-Time" : "Recurring"}
                    </span>
                  </td>
                  <td className="px-5 py-3.5 text-gray-500">
                    {formatDuration(m.durationSeconds)}
                  </td>
                  <td className="px-5 py-3.5 text-gray-500">
                    {m.isGlobal ? <span className="text-xs text-indigo-600 font-medium">All</span> : m.serviceSlugs.length}
                  </td>
                  <td className="px-5 py-3.5 text-gray-500 whitespace-nowrap text-xs">
                    {formatNextEvent(m)}
                  </td>
                  <td className="px-5 py-3.5">
                    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATUS_BADGE[m.displayStatus]}`}>
                      {m.displayStatus}
                    </span>
                  </td>
                  <td className="px-5 py-3.5">
                    <button
                      onClick={() => navigate(ROUTES.MAINTENANCES.DETAIL(m.id))}
                      className="rounded-md p-1.5 text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
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
          <div className="flex items-center justify-between text-sm text-gray-500">
            <span>
              {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, filtered.length)} of {filtered.length}
            </span>
            <div className="flex gap-2">
              <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}
                className="rounded-lg border border-border bg-background px-3 py-1.5 text-sm hover:bg-muted disabled:opacity-40">
                Previous
              </button>
              <button disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}
                className="rounded-lg border border-border bg-background px-3 py-1.5 text-sm hover:bg-muted disabled:opacity-40">
                Next
              </button>
            </div>
          </div>
        )}
      </div>
    </>
  );
}
