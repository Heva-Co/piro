import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, Pencil } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { incidentsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const STATE_BADGE: Record<string, string> = {
  investigating: "bg-amber-100 text-amber-700",
  identified: "bg-orange-100 text-orange-700",
  monitoring: "bg-blue-100 text-blue-700",
  resolved: "bg-green-100 text-green-700",
  INVESTIGATING: "bg-amber-100 text-amber-700",
  IDENTIFIED: "bg-orange-100 text-orange-700",
  MONITORING: "bg-blue-100 text-blue-700",
  RESOLVED: "bg-green-100 text-green-700",
};

const PAGE_SIZE = 10;

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
    if (stateFilter === "active") return inc.status?.toLowerCase() !== "resolved";
    if (stateFilter === "resolved") return inc.status?.toLowerCase() === "resolved";
    return true;
  });

  const totalPages = Math.ceil(filtered.length / PAGE_SIZE);
  const paged = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  function formatDuration(start: string, end?: string) {
    const s = new Date(start).getTime();
    const e = end ? new Date(end).getTime() : Date.now();
    const diff = Math.round((e - s) / 60000);
    if (diff < 60) return `${diff}m`;
    const h = Math.floor(diff / 60);
    const m = diff % 60;
    return `${h}h ${m}m`;
  }

  return (
    <AdminLayout title="Incidents">
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between">
          <div className="flex gap-2">
            {[
              { label: "All", value: "all" },
              { label: "Active", value: "active" },
              { label: "Resolved", value: "resolved" },
            ].map((f) => (
              <button
                key={f.value}
                onClick={() => { setStateFilter(f.value); setPage(1); }}
                className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                  stateFilter === f.value
                    ? "bg-indigo-600 text-white"
                    : "border border-gray-300 bg-white text-gray-600 hover:bg-gray-50"
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>
          <button
            onClick={() => navigate(ROUTES.INCIDENTS.NEW)}
            className="flex items-center gap-2 rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
          >
            <Plus size={16} /> New Incident
          </button>
        </div>

        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow-sm">
          <table className="min-w-full text-sm">
            <thead className="border-b border-gray-200 bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-gray-500 w-16">ID</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Title</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Start Time</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Duration</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">State</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Services</th>
                <th className="px-4 py-3 w-12"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading && (
                <tr>
                  <td colSpan={7} className="px-4 py-6 text-center text-gray-400">Loading…</td>
                </tr>
              )}
              {!isLoading && paged.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-6 text-center text-gray-400">No incidents found.</td>
                </tr>
              )}
              {paged.map((inc) => (
                <tr key={inc.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-gray-400 font-mono text-xs">#{inc.id}</td>
                  <td className="px-4 py-3 font-medium">{inc.title}</td>
                  <td className="px-4 py-3 text-gray-500 whitespace-nowrap">
                    {new Date(inc.startedAt).toLocaleString()}
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    {formatDuration(inc.startedAt, inc.resolvedAt)}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium capitalize ${STATE_BADGE[inc.status] ?? "bg-gray-100 text-gray-600"}`}>
                      {inc.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-500">{inc.services?.length ?? 0}</td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => navigate(ROUTES.INCIDENTS.DETAIL(inc.id))}
                      className="rounded p-1 text-gray-400 hover:text-indigo-600 hover:bg-indigo-50 transition-colors"
                    >
                      <Pencil size={15} />
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
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-md border border-gray-300 bg-white px-3 py-1.5 hover:bg-gray-50 disabled:opacity-40"
              >
                Previous
              </button>
              <button
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-md border border-gray-300 bg-white px-3 py-1.5 hover:bg-gray-50 disabled:opacity-40"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
