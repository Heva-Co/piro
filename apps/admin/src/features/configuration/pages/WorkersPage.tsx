import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Trash2 } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { workersApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

const STATUS_COLORS: Record<string, string> = {
  ONLINE: "bg-green-100 text-green-700",
  OFFLINE: "bg-gray-100 text-gray-500",
  ERROR: "bg-red-100 text-red-700",
};

export default function WorkersPage() {
  const qc = useQueryClient();
  const { data: workers = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.WORKERS,
    queryFn: workersApi.list,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => workersApi.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.WORKERS }),
  });

  return (
    <AdminLayout title="Workers">
      <div className="flex flex-col gap-4">
        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow-sm">
          <table className="min-w-full text-sm">
            <thead className="border-b border-gray-200 bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-gray-500">ID</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Name</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Status</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Last Seen</th>
                <th className="px-4 py-3 w-12"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-gray-400">Loading…</td>
                </tr>
              )}
              {!isLoading && workers.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-gray-400">No workers registered.</td>
                </tr>
              )}
              {workers.map((w) => (
                <tr key={w.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-mono text-xs text-gray-500">{w.id}</td>
                  <td className="px-4 py-3 font-medium">{w.name}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[w.status] ?? "bg-gray-100 text-gray-500"}`}>
                      {w.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    {w.lastSeenAt ? new Date(w.lastSeenAt).toLocaleString() : "—"}
                  </td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => {
                        if (confirm(`Remove worker "${w.name}"?`)) {
                          deleteMutation.mutate(w.id);
                        }
                      }}
                      className="rounded p-1 text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors"
                    >
                      <Trash2 size={15} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </AdminLayout>
  );
}
