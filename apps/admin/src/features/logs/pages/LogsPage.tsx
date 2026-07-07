import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { AutoRefreshButton } from "@/components/AutoRefreshButton";
import { DateTimePicker } from "@/components/DateTimePicker";
import { logsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

const LEVEL_COLORS: Record<string, string> = {
  DEBUG: "bg-muted text-foreground",
  INFO: "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  WARNING: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  WARN: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  ERROR: "bg-red-100 text-red-700",
};

export default function LogsPage() {
  const [level, setLevel] = useState("");
  const [source, setSource] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());

  const params = {
    page,
    pageSize,
    ...(level ? { level } : {}),
    ...(source ? { source } : {}),
    ...(from ? { from } : {}),
    ...(to ? { to } : {}),
  };

  const { data, isLoading, refetch } = useQuery({
    queryKey: QUERY_KEYS.LOGS(params),
    queryFn: () => logsApi.list(params),
  });

  const items = data?.items ?? [];
  const total = data?.totalCount ?? 0;
  const totalPages = Math.ceil(total / pageSize);

  function toggleExpand(id: number) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  return (
    <AdminLayout title="Logs">
      <div className="flex flex-col gap-4">
        {/* Filters */}
        <div className="flex flex-wrap gap-3 items-end">
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted-foreground">Level</label>
            <select
              value={level}
              onChange={(e) => { setLevel(e.target.value); setPage(1); }}
              className="rounded-md border border-border bg-background px-3 py-1.5 text-sm"
            >
              <option value="">All</option>
              <option value="DEBUG">Debug</option>
              <option value="INFO">Info</option>
              <option value="WARNING">Warning</option>
              <option value="ERROR">Error</option>
            </select>
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted-foreground">Source</label>
            <input
              type="text"
              value={source}
              onChange={(e) => { setSource(e.target.value); setPage(1); }}
              placeholder="Filter by source…"
              className="rounded-md border border-border bg-background px-3 py-1.5 text-sm w-44"
            />
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted-foreground">From</label>
            <DateTimePicker value={from} onChange={(v) => { setFrom(v); setPage(1); }} />
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted-foreground">To</label>
            <DateTimePicker value={to} onChange={(v) => { setTo(v); setPage(1); }} />
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted-foreground">Per page</label>
            <select
              value={pageSize}
              onChange={(e) => { setPageSize(Number(e.target.value)); setPage(1); }}
              className="rounded-md border border-border bg-background px-3 py-1.5 text-sm"
            >
              {[25, 50, 100, 200].map((n) => (
                <option key={n} value={n}>{n}</option>
              ))}
            </select>
          </div>

          <AutoRefreshButton onRefetch={refetch} />
        </div>

        {/* Table */}
        <div className="overflow-x-auto rounded-lg border border-border bg-card shadow-sm">
          <table className="min-w-full text-sm">
            <thead className="border-b border-border bg-muted/30">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground w-40">Time</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground w-24">Level</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground w-36">Source</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Message</th>
                <th className="px-4 py-3 w-8"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {isLoading && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">
                    Loading…
                  </td>
                </tr>
              )}
              {!isLoading && items.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">
                    No logs found.
                  </td>
                </tr>
              )}
              {items.map((log) => (
                <>
                  <tr
                    key={log.id}
                    className="hover:bg-muted cursor-pointer"
                    onClick={() => log.metadata && toggleExpand(log.id)}
                  >
                    <td className="px-4 py-2.5 text-muted-foreground whitespace-nowrap font-mono text-xs">
                      {new Date(log.timestamp).toLocaleString()}
                    </td>
                    <td className="px-4 py-2.5">
                      <span
                        className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${
                          LEVEL_COLORS[log.level.toUpperCase()] ?? "bg-muted text-foreground"
                        }`}
                      >
                        {log.level}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-muted-foreground text-xs truncate max-w-[9rem]">
                      {log.source ?? "—"}
                    </td>
                    <td className="px-4 py-2.5 text-foreground">{log.message}</td>
                    <td className="px-4 py-2.5">
                      {log.metadata && (
                        <ChevronDown
                          size={14}
                          className={`text-muted-foreground transition-transform ${
                            expanded.has(log.id) ? "rotate-180" : ""
                          }`}
                        />
                      )}
                    </td>
                  </tr>
                  {log.metadata && expanded.has(log.id) && (
                    <tr key={`${log.id}-meta`} className="bg-muted/30">
                      <td colSpan={5} className="px-4 py-3">
                        <pre className="text-xs font-mono text-muted-foreground whitespace-pre-wrap">
                          {JSON.stringify(log.metadata, null, 2)}
                        </pre>
                      </td>
                    </tr>
                  )}
                </>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>
              {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} of {total}
            </span>
            <div className="flex gap-2">
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-md border border-border bg-background px-3 py-1.5 hover:bg-muted disabled:opacity-40"
              >
                Previous
              </button>
              <button
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-md border border-border bg-background px-3 py-1.5 hover:bg-muted disabled:opacity-40"
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
