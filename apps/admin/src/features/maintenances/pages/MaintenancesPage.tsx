import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, Pencil } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { maintenancesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const PAGE_SIZE = 10;

const STATUS_BADGE: Record<string, string> = {
  ACTIVE: "bg-green-100 text-green-700",
  active: "bg-green-100 text-green-700",
  CANCELLED: "bg-gray-100 text-gray-500",
  cancelled: "bg-gray-100 text-gray-500",
  SCHEDULED: "bg-blue-100 text-blue-700",
  scheduled: "bg-blue-100 text-blue-700",
  COMPLETED: "bg-indigo-100 text-indigo-700",
  completed: "bg-indigo-100 text-indigo-700",
};

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
    if (statusFilter === "active") return m.status?.toLowerCase() !== "cancelled";
    if (statusFilter === "cancelled") return m.status?.toLowerCase() === "cancelled";
    return true;
  });

  const totalPages = Math.ceil(filtered.length / PAGE_SIZE);
  const paged = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  function formatDuration(start: string, end: string) {
    const diff = Math.round((new Date(end).getTime() - new Date(start).getTime()) / 60000);
    if (diff < 60) return `${diff}m`;
    const h = Math.floor(diff / 60);
    const m = diff % 60;
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  function isRecurring(_m: typeof maintenances[0]) {
    return false;
  }

  return (
    <AdminLayout title="Maintenances">
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between">
          <div className="flex gap-2">
            {[
              { label: "All", value: "all" },
              { label: "Active", value: "active" },
              { label: "Cancelled", value: "cancelled" },
            ].map((f) => (
              <button
                key={f.value}
                onClick={() => { setStatusFilter(f.value); setPage(1); }}
                className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                  statusFilter === f.value
                    ? "bg-indigo-600 text-white"
                    : "border border-gray-300 bg-white text-gray-600 hover:bg-gray-50"
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>
          <button
            onClick={() => navigate(ROUTES.MAINTENANCES.NEW)}
            className="flex items-center gap-2 rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
          >
            <Plus size={16} /> New Maintenance
          </button>
        </div>

        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow-sm">
          <table className="min-w-full text-sm">
            <thead className="border-b border-gray-200 bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-gray-500 w-16">ID</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Title</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Type</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Duration</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Services</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Scheduled Start</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Status</th>
                <th className="px-4 py-3 w-12"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading && (
                <tr>
                  <td colSpan={8} className="px-4 py-6 text-center text-gray-400">Loading…</td>
                </tr>
              )}
              {!isLoading && paged.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-4 py-6 text-center text-gray-400">No maintenances found.</td>
                </tr>
              )}
              {paged.map((m) => (
                <tr key={m.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-gray-400 font-mono text-xs">#{m.id}</td>
                  <td className="px-4 py-3 font-medium">{m.name}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${isRecurring(m) ? "bg-purple-100 text-purple-700" : "bg-blue-100 text-blue-700"}`}>
                      {isRecurring(m) ? "Recurring" : "One-Time"}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    {formatDuration(m.scheduledStart, m.scheduledEnd)}
                  </td>
                  <td className="px-4 py-3 text-gray-500">{m.services?.length ?? 0}</td>
                  <td className="px-4 py-3 text-gray-500 whitespace-nowrap">
                    {new Date(m.scheduledStart).toLocaleString()}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium capitalize ${STATUS_BADGE[m.status] ?? "bg-gray-100 text-gray-500"}`}>
                      {m.status}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => navigate(ROUTES.MAINTENANCES.DETAIL(m.id))}
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
