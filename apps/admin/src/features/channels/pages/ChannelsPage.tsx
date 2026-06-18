import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, AlertTriangle, Settings } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { channelsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const TYPE_COLORS: Record<string, string> = {
  Webhook: "bg-purple-100 text-purple-700",
  Email: "bg-blue-100 text-blue-700",
  Slack: "bg-green-100 text-green-700",
  PagerDuty: "bg-orange-100 text-orange-700",
  MSTeams: "bg-indigo-100 text-indigo-700",
  Telegram: "bg-sky-100 text-sky-700",
  TwilioSms: "bg-red-100 text-red-700",
  GoogleChat: "bg-yellow-100 text-yellow-700",
  Discord: "bg-violet-100 text-violet-700",
  Opsgenie: "bg-amber-100 text-amber-700",
  Pushover: "bg-rose-100 text-rose-700",
  Ntfy: "bg-teal-100 text-teal-700",
};

export default function ChannelsPage() {
  const navigate = useNavigate();
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all");

  const { data: channels = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.CHANNELS,
    queryFn: channelsApi.list,
  });

  const filtered = channels.filter((c) => {
    if (statusFilter === "active") return c.isActive;
    if (statusFilter === "inactive") return !c.isActive;
    return true;
  });

  return (
    <AdminLayout title="Notification Channels">
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between">
          <div className="flex gap-2">
            {(["all", "active", "inactive"] as const).map((f) => (
              <button
                key={f}
                onClick={() => setStatusFilter(f)}
                className={`rounded-md px-3 py-1.5 text-sm font-medium capitalize transition-colors ${
                  statusFilter === f
                    ? "bg-indigo-600 text-white"
                    : "border border-gray-300 bg-white text-gray-600 hover:bg-gray-50"
                }`}
              >
                {f}
              </button>
            ))}
          </div>
          <button
            onClick={() => navigate(ROUTES.CHANNELS.NEW)}
            className="flex items-center gap-2 rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
          >
            <Plus size={16} /> New Channel
          </button>
        </div>

        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow-sm">
          <table className="min-w-full text-sm">
            <thead className="border-b border-gray-200 bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Name</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Type</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Status</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Flags</th>
                <th className="px-4 py-3 w-12"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-gray-400">Loading…</td>
                </tr>
              )}
              {!isLoading && filtered.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-gray-400">No channels found.</td>
                </tr>
              )}
              {filtered.map((ch) => {
                const cfg = ch.config as Record<string, unknown>;
                const isGlobal = cfg?.isGlobal as boolean | undefined;
                const isLocked = cfg?.isLocked as boolean | undefined;
                return (
                  <tr key={ch.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-medium">{ch.name}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${TYPE_COLORS[ch.type] ?? "bg-gray-100 text-gray-700"}`}>
                        {ch.type}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${ch.isActive ? "bg-green-100 text-green-700" : "bg-gray-100 text-gray-500"}`}>
                        {ch.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex gap-1 flex-wrap">
                        {isGlobal && (
                          <span className="inline-flex items-center rounded px-2 py-0.5 text-xs bg-indigo-100 text-indigo-700">Global</span>
                        )}
                        {isLocked && (
                          <span className="inline-flex items-center rounded px-2 py-0.5 text-xs bg-amber-100 text-amber-700">Locked</span>
                        )}
                        {!isGlobal && (
                          <span className="inline-flex items-center gap-1 rounded px-2 py-0.5 text-xs bg-orange-50 text-orange-600">
                            <AlertTriangle size={11} /> No alerts linked
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <button
                        onClick={() => navigate(ROUTES.CHANNELS.DETAIL(ch.id))}
                        className="rounded p-1 text-gray-400 hover:text-indigo-600 hover:bg-indigo-50 transition-colors"
                        title="Configure"
                      >
                        <Settings size={15} />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </AdminLayout>
  );
}
