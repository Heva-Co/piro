import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { RefreshCw, Settings, ChevronLeft } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { useCheck } from "@/hooks/useChecks";
import { checksApi } from "@/lib/api";
import { ROUTES } from "@/constants/routes";

// ── Skeleton ──────────────────────────────────────────────────────────────────

function Skeleton({ className }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-muted ${className ?? ""}`} />;
}

// ── Status pill ───────────────────────────────────────────────────────────────

function StatusPill({ status }: { status: string }) {
  const s = (status ?? "").toLowerCase();
  const isUp = s === "up";
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
      isUp ? "bg-foreground text-background" : "bg-destructive text-destructive-foreground"
    }`}>
      {s.toUpperCase()}
    </span>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

const PAGE_SIZE_OPTIONS = [20, 50, 100, 200];

export default function CheckLogsPage() {
  const { slug: serviceSlug, checkSlug } = useParams<{ slug: string; checkSlug: string }>();
  const navigate = useNavigate();

  const { data: check } = useCheck(serviceSlug!, checkSlug!);

  const [limit, setLimit] = useState(50);
  const [region, setRegion] = useState("");
  const [statusFilter, setStatusFilter] = useState<"" | "UP" | "DOWN">("");
  const [page, setPage] = useState(1);

  const { data: logs, isLoading, isFetching, refetch } = useQuery({
    queryKey: ["check-logs-full", serviceSlug, checkSlug, limit, region],
    queryFn: () => checksApi.logs(serviceSlug!, checkSlug!, {
      limit,
      region: region || undefined,
    }),
    enabled: !!serviceSlug && !!checkSlug,
  });

  // Client-side status filter + pagination
  const filtered = (logs ?? []).filter((l) => {
    if (statusFilter && l.status.toUpperCase() !== statusFilter) return false;
    return true;
  });

  const totalPages = Math.max(1, Math.ceil(filtered.length / 20));
  const paginated = filtered.slice((page - 1) * 20, page * 20);

  // Collect unique regions from data
  const regions = Array.from(new Set((logs ?? []).map((l) => l.workerRegion).filter(Boolean)));

  function handleLimitChange(newLimit: number) {
    setLimit(newLimit);
    setPage(1);
  }

  function handleRegionChange(r: string) {
    setRegion(r);
    setPage(1);
  }

  function handleStatusChange(s: "" | "UP" | "DOWN") {
    setStatusFilter(s);
    setPage(1);
  }

  return (
    <AdminLayout title="Logs">
      <div className="flex flex-col gap-6">
        {/* Breadcrumb */}
        <div className="flex items-center justify-between">
          <nav className="flex items-center gap-2 text-sm text-muted-foreground">
            <button
              onClick={() => navigate(ROUTES.SERVICES.LIST)}
              className="hover:text-foreground transition-colors"
            >
              Services
            </button>
            <span>/</span>
            <button
              onClick={() => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!))}
              className="hover:text-foreground transition-colors"
            >
              {serviceSlug}
            </button>
            <span>/</span>
            <button
              onClick={() => navigate(ROUTES.CHECKS.DETAIL(serviceSlug!, checkSlug!))}
              className="hover:text-foreground transition-colors"
            >
              {check?.name ?? checkSlug}
            </button>
            <span>/</span>
            <span className="text-foreground font-medium">Logs</span>
          </nav>

          <div className="flex items-center gap-2">
            <button
              onClick={() => navigate(ROUTES.CHECKS.DETAIL(serviceSlug!, checkSlug!))}
              className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors"
            >
              <Settings size={14} />
              Configure
            </button>
            <button
              onClick={() => refetch()}
              disabled={isFetching}
              className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors"
            >
              <RefreshCw size={14} className={isFetching ? "animate-spin" : ""} />
              Refresh
            </button>
          </div>
        </div>

        {/* Check info */}
        {check && (
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-bold">{check.name}</h1>
            <span className="rounded-full border px-2.5 py-0.5 text-xs font-medium">
              {check.type.toUpperCase()}
            </span>
            <StatusPill status={check.currentStatus} />
          </div>
        )}

        {/* Filters */}
        <div className="flex items-center gap-3 flex-wrap">
          <div className="flex items-center gap-2">
            <label className="text-sm text-muted-foreground">Status</label>
            <select
              value={statusFilter}
              onChange={(e) => handleStatusChange(e.target.value as "" | "UP" | "DOWN")}
              className="rounded-lg border bg-background px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-ring"
            >
              <option value="">All</option>
              <option value="UP">Up</option>
              <option value="DOWN">Down</option>
            </select>
          </div>

          <div className="flex items-center gap-2">
            <label className="text-sm text-muted-foreground">Region</label>
            <select
              value={region}
              onChange={(e) => handleRegionChange(e.target.value)}
              className="rounded-lg border bg-background px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-ring"
            >
              <option value="">All regions</option>
              {regions.map((r) => (
                <option key={r} value={r}>{r}</option>
              ))}
            </select>
          </div>

          <div className="flex items-center gap-2 ml-auto">
            <label className="text-sm text-muted-foreground">Load last</label>
            <select
              value={limit}
              onChange={(e) => handleLimitChange(Number(e.target.value))}
              className="rounded-lg border bg-background px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-ring"
            >
              {PAGE_SIZE_OPTIONS.map((n) => (
                <option key={n} value={n}>{n} entries</option>
              ))}
            </select>
          </div>
        </div>

        {/* Table */}
        <div className="rounded-xl border bg-card overflow-hidden">
          {isLoading ? (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/40">
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Time</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Latency</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Region</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Message</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {Array.from({ length: 8 }).map((_, i) => (
                  <tr key={i}>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-36" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-5 w-12 rounded-full" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-16" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-16" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-48" /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : paginated.length === 0 ? (
            <div className="px-5 py-12 text-sm text-muted-foreground text-center">
              No logs match your filters.
            </div>
          ) : (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/40">
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Time</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Latency</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Region</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Message</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {paginated.map((log, i) => (
                  <tr key={`${log.timestamp}-${i}`} className="hover:bg-muted/30 transition-colors">
                    <td className="px-5 py-2.5 text-xs text-muted-foreground whitespace-nowrap">
                      {new Date(log.timestamp).toLocaleString()}
                    </td>
                    <td className="px-5 py-2.5">
                      <StatusPill status={log.status} />
                    </td>
                    <td className="px-5 py-2.5 text-sm text-muted-foreground">
                      {log.latencyMs != null ? `${Math.round(log.latencyMs)} ms` : "—"}
                    </td>
                    <td className="px-5 py-2.5 text-xs text-muted-foreground">{log.workerRegion ?? "—"}</td>
                    <td className="px-5 py-2.5 text-xs text-muted-foreground">{log.errorMessage ?? ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Pagination */}
        {!isLoading && totalPages > 1 && (
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>
              Showing {(page - 1) * 20 + 1}–{Math.min(page * 20, filtered.length)} of {filtered.length} entries
            </span>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
                className="rounded-lg border px-3 py-1.5 text-sm hover:bg-muted disabled:opacity-40 transition-colors"
              >
                <ChevronLeft size={14} />
              </button>
              {Array.from({ length: Math.min(7, totalPages) }, (_, i) => {
                // Show pages around current
                let p = i + 1;
                if (totalPages > 7) {
                  const start = Math.max(1, Math.min(page - 3, totalPages - 6));
                  p = start + i;
                }
                return (
                  <button
                    key={p}
                    onClick={() => setPage(p)}
                    className={`rounded-lg border px-3 py-1.5 text-sm transition-colors ${
                      p === page ? "bg-foreground text-background border-foreground" : "hover:bg-muted"
                    }`}
                  >
                    {p}
                  </button>
                );
              })}
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
                className="rounded-lg border px-3 py-1.5 text-sm hover:bg-muted disabled:opacity-40 transition-colors"
              >
                <ChevronLeft size={14} className="rotate-180" />
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
