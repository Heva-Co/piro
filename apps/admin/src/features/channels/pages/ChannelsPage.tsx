import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus } from "lucide-react";
import { channelsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { CHANNEL_TYPE_MAP } from "@/constants/channels";
import { Icon } from "@iconify/react";

function Skeleton({ className }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-muted ${className ?? ""}`} />;
}

export default function ChannelsPage() {
  const navigate = useNavigate();
  const [statusFilter, setStatusFilter] = useState<"All" | "Active" | "Inactive">("All");

  const { data: channels = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.CHANNELS,
    queryFn: channelsApi.list,
  });

  const filtered = channels.filter((c) => {
    if (statusFilter === "Active") return !c.isInactive;
    if (statusFilter === "Inactive") return c.isInactive;
    return true;
  });

  return (
    <>
      <div className="flex flex-col gap-6">
        {/* Header */}
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-xl font-bold">Notification Channels</h1>
            <p className="text-sm text-muted-foreground mt-0.5">
              Configure where alerts are sent when a check fails or recovers.
            </p>
          </div>
          <button
            onClick={() => navigate(ROUTES.CHANNELS.NEW)}
            className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <Plus size={14} /> New Channel
          </button>
        </div>

        {/* Status filter */}
        <div className="flex items-center gap-2">
          <span className="text-sm text-muted-foreground">Status</span>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
            className="rounded-lg border bg-background px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-ring"
          >
            {(["All", "Active", "Inactive"] as const).map((f) => (
              <option key={f} value={f}>{f}</option>
            ))}
          </select>
        </div>

        {/* Table / empty state */}
        <div className="rounded-xl border bg-card overflow-hidden">
          {isLoading ? (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/40">
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Name</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Type</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
                  <th className="px-5 py-2.5" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {Array.from({ length: 3 }).map((_, i) => (
                  <tr key={i}>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-32" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-5 w-20 rounded-full" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-5 w-16 rounded-full" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-20 ml-auto" /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : filtered.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 gap-4">
              <p className="text-sm text-muted-foreground">No notification channels yet.</p>
              <button
                onClick={() => navigate(ROUTES.CHANNELS.NEW)}
                className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
              >
                <Plus size={14} /> Create your first channel
              </button>
            </div>
          ) : (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/40">
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Name</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Type</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
                  <th className="px-5 py-2.5" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {filtered.map((ch) => (
                  <tr key={ch.id} className="hover:bg-muted/30 transition-colors">
                    <td className="px-5 py-3 font-medium">{ch.name}</td>
                    <td className="px-5 py-3">
                      <span className="inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium">
                        {CHANNEL_TYPE_MAP[ch.type as keyof typeof CHANNEL_TYPE_MAP]?.icon && (
                          <Icon
                            icon={CHANNEL_TYPE_MAP[ch.type as keyof typeof CHANNEL_TYPE_MAP].icon}
                            className={`size-3.5 ${CHANNEL_TYPE_MAP[ch.type as keyof typeof CHANNEL_TYPE_MAP].iconClass ?? ""}`}
                          />
                        )}
                        {CHANNEL_TYPE_MAP[ch.type as keyof typeof CHANNEL_TYPE_MAP]?.label ?? ch.type}
                      </span>
                    </td>
                    <td className="px-5 py-3">
                      <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                        !ch.isInactive
                          ? "bg-green-500/10 text-green-600 dark:text-green-400 border border-green-500/30"
                          : "bg-muted text-muted-foreground border border-border"
                      }`}>
                        {!ch.isInactive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-right">
                      <button
                        onClick={() => navigate(ROUTES.CHANNELS.DETAIL(ch.id))}
                        className="rounded-lg border px-3 py-1 text-sm font-medium hover:bg-muted transition-colors"
                      >
                        Configure
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  );
}
