import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, Pencil, ChevronDown } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { incidentsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const STATE_BADGE: Record<string, string> = {
  INVESTIGATING: "bg-amber-100 text-amber-700",
  IDENTIFIED:    "bg-orange-100 text-orange-700",
  MONITORING:    "bg-blue-100 text-blue-700",
  RESOLVED:      "bg-green-100 text-green-700",
};

const PAGE_SIZE = 10;

const FILTER_OPTIONS = [
  { label: "All States",   value: "all" },
  { label: "Investigating", value: "investigating" },
  { label: "Identified",   value: "identified" },
  { label: "Monitoring",   value: "monitoring" },
  { label: "Resolved",     value: "resolved" },
];

function formatDuration(start: string, end?: string) {
  const diff = Math.round(((end ? new Date(end).getTime() : Date.now()) - new Date(start).getTime()) / 60000);
  if (diff < 60) return `${diff}m`;
  const h = Math.floor(diff / 60);
  const m = diff % 60;
  return `${h}h ${m}m`;
}

export default function IncidentsPage() {
  const navigate = useNavigate();
  const [stateFilter, setStateFilter] = useState("all");
  const [page, setPage] = useState(1);

  const { data: incidents = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.INCIDENTS,
    queryFn: incidentsApi.list,
  });

  const filtered = incidents.filter((inc) => {
    if (stateFilter === "all") return true;
    return inc.status?.toLowerCase() === stateFilter;
  });

  const totalPages = Math.ceil(filtered.length / PAGE_SIZE);
  const paged = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <AdminLayout title="Incidents">
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between">
          {/* Filter dropdown */}
          <div className="relative">
            <select
              value={stateFilter}
              onChange={(e) => { setStateFilter(e.target.value); setPage(1); }}
              className="appearance-none rounded-lg border border-gray-300 bg-white pl-3 pr-8 py-2 text-sm font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-gray-400 cursor-pointer"
            >
              {FILTER_OPTIONS.map(f => (
                <option key={f.value} value={f.value}>{f.label}</option>
              ))}
            </select>
            <ChevronDown size={14} className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-500" />
          </div>

          <button
            onClick={() => navigate(ROUTES.INCIDENTS.NEW)}
            className="flex items-center gap-2 rounded-lg bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800"
          >
            <Plus size={15} /> New Incident
          </button>
        </div>

        <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
          <table className="min-w-full text-sm">
            <thead className="border-b border-gray-100 bg-gray-50">
              <tr>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide w-16">ID</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Title</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Duration</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">State</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Affects</th>
                <th className="px-5 py-3 w-12"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading && (
                <tr><td colSpan={6} className="px-5 py-10 text-center text-sm text-gray-400">Loading…</td></tr>
              )}
              {!isLoading && paged.length === 0 && (
                <tr><td colSpan={6} className="px-5 py-10 text-center text-sm text-gray-400">No incidents found.</td></tr>
              )}
              {paged.map((inc) => (
                <tr key={inc.id} className="hover:bg-gray-50">
                  <td className="px-5 py-3.5 text-gray-400 font-mono text-xs">#{inc.id}</td>
                  <td className="px-5 py-3.5 font-medium text-gray-900">{inc.title}</td>
                  <td className="px-5 py-3.5 text-gray-500 text-xs">
                    {formatDuration(inc.startedAt, inc.resolvedAt)}
                  </td>
                  <td className="px-5 py-3.5">
                    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold capitalize ${STATE_BADGE[inc.status?.toUpperCase()] ?? "bg-gray-100 text-gray-600"}`}>
                      {inc.status?.toLowerCase()}
                    </span>
                  </td>
                  <td className="px-5 py-3.5 text-gray-500 text-sm">
                    {inc.isGlobal
                      ? <span className="text-xs text-indigo-600 font-medium">All</span>
                      : (inc.services?.length ?? 0)}
                  </td>
                  <td className="px-5 py-3.5">
                    <button
                      onClick={() => navigate(ROUTES.INCIDENTS.DETAIL(inc.id))}
                      className="rounded-md p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors"
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
            <span>{(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, filtered.length)} of {filtered.length}</span>
            <div className="flex gap-2">
              <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}
                className="rounded-lg border border-gray-300 bg-white px-3 py-1.5 text-sm hover:bg-gray-50 disabled:opacity-40">Previous</button>
              <button disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}
                className="rounded-lg border border-gray-300 bg-white px-3 py-1.5 text-sm hover:bg-gray-50 disabled:opacity-40">Next</button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
