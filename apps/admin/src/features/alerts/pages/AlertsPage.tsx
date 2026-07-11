import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Search, ExternalLink, ChevronLeft, ChevronRight } from "lucide-react";
import { AutoRefreshButton } from "@/components/AutoRefreshButton";
import { StatusPill } from "@/components/StatusBadge";
import { useAllAlerts } from "@/hooks/useChecks";
import { ROUTES } from "@/constants/routes";

const PAGE_SIZE = 50;

// ── Skeleton ──────────────────────────────────────────────────────────────────

function Skeleton({ className }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-muted ${className ?? ""}`} />;
}

// ── Stat card ─────────────────────────────────────────────────────────────────

function StatCard({ label, value, color }: { label: string; value: number; color?: string }) {
  return (
    <div className="rounded-xl border bg-card p-5 flex flex-col gap-2">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className={`text-3xl font-bold ${color ?? ""}`}>{value}</p>
    </div>
  );
}

function StatCardSkeleton() {
  return (
    <div className="rounded-xl border bg-card p-5 flex flex-col gap-2">
      <Skeleton className="h-4 w-16" />
      <Skeleton className="h-9 w-10 mt-1" />
    </div>
  );
}

function formatDate(value?: string) {
  if (!value) return "—";
  return new Date(value).toLocaleString();
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function AlertsPage() {
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const { data, isLoading, refetch } = useAllAlerts({ page, pageSize: PAGE_SIZE });
  const alerts = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const filtered = alerts.filter((a) => {
    const q = search.toLowerCase();
    return (
      a.checkName.toLowerCase().includes(q) ||
      a.serviceName.toLowerCase().includes(q) ||
      (a.message ?? "").toLowerCase().includes(q)
    );
  });

  // Active/linked counts reflect only the currently loaded page — Alert history can span
  // thousands of rows, so these are not meant as instance-wide totals.
  const activeOnPage = alerts.filter((a) => !a.resolvedAt).length;
  const linkedOnPage = alerts.filter((a) => !!a.incidentId).length;

  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div>
        <h1 className="text-xl font-bold">Alerts</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Alert history across every check — independent of whether they escalated to an incident.
        </p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-3 gap-4">
        {isLoading ? (
          <>
            <StatCardSkeleton />
            <StatCardSkeleton />
            <StatCardSkeleton />
          </>
        ) : (
          <>
            <StatCard label="Total" value={totalCount} />
            <StatCard label="Active (this page)" value={activeOnPage} color="text-red-600" />
            <StatCard label="Linked to incident (this page)" value={linkedOnPage} color="text-amber-600" />
          </>
        )}
      </div>

      {/* Table card */}
      <div className="rounded-xl border bg-card overflow-hidden">
        {/* Search + refresh */}
        <div className="px-4 py-3 border-b flex items-center gap-3">
          <div className="flex flex-1 items-center gap-2.5 rounded-lg border bg-background px-3 py-2 text-sm">
            <Search size={14} className="text-muted-foreground shrink-0" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search checks, services, messages..."
              className="flex-1 bg-transparent outline-none text-sm"
            />
          </div>
          <AutoRefreshButton onRefetch={refetch} />
        </div>

        {isLoading ? (
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b">
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Impact</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Check</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Service</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Message</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Occurrences</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Fired At</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Resolved At</th>
                <th className="px-5 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {Array.from({ length: 4 }).map((_, i) => (
                <tr key={i}>
                  <td className="px-5 py-3"><Skeleton className="h-6 w-16 rounded-full" /></td>
                  <td className="px-5 py-3"><Skeleton className="h-4 w-36" /></td>
                  <td className="px-5 py-3"><Skeleton className="h-4 w-28" /></td>
                  <td className="px-5 py-3"><Skeleton className="h-4 w-48" /></td>
                  <td className="px-5 py-3"><Skeleton className="h-4 w-8" /></td>
                  <td className="px-5 py-3"><Skeleton className="h-4 w-32" /></td>
                  <td className="px-5 py-3"><Skeleton className="h-4 w-32" /></td>
                  <td className="px-5 py-3"><Skeleton className="h-4 w-12 ml-auto" /></td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : filtered.length === 0 ? (
          <div className="px-5 py-8 text-sm text-muted-foreground text-center">
            {search ? "No alerts match your search." : "No alerts recorded yet."}
          </div>
        ) : (
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b">
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Impact</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Check</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Service</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Message</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Occurrences</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Fired At</th>
                <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Resolved At</th>
                <th className="px-5 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {filtered.map((alert) => (
                <tr key={alert.id} className="hover:bg-muted/30 transition-colors">
                  <td className="px-5 py-3">
                    <StatusPill status={alert.impactAtFireTime} />
                  </td>
                  <td className="px-5 py-3 font-semibold">
                    <Link to={ROUTES.ALERTS.DETAIL(alert.id)} className="hover:underline">
                      {alert.checkName}
                    </Link>
                  </td>
                  <td className="px-5 py-3 text-muted-foreground">{alert.serviceName}</td>
                  <td className="px-5 py-3 text-muted-foreground max-w-xs truncate" title={alert.message}>
                    {alert.message ?? "—"}
                  </td>
                  <td className="px-5 py-3">{alert.occurrenceCount}</td>
                  <td className="px-5 py-3 text-muted-foreground">{formatDate(alert.firedAt)}</td>
                  <td className={`px-5 py-3 ${alert.resolvedAt ? "text-muted-foreground" : "text-red-600 font-medium"}`}>
                    {alert.resolvedAt ? formatDate(alert.resolvedAt) : "Active"}
                  </td>
                  <td className="px-5 py-3">
                    {alert.incidentId != null && (
                      <div className="flex items-center justify-end">
                        <Link
                          to={ROUTES.INCIDENTS.DETAIL(alert.incidentId)}
                          title="View linked incident"
                          className="text-muted-foreground hover:text-foreground transition-colors"
                        >
                          <ExternalLink size={16} />
                        </Link>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {/* Pagination */}
        {!isLoading && totalCount > 0 && (
          <div className="px-4 py-3 border-t flex items-center justify-between text-sm">
            <span className="text-muted-foreground">
              Page {page} of {totalPages} · {totalCount} alert{totalCount === 1 ? "" : "s"}
            </span>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="flex items-center gap-1 rounded-lg border px-3 py-1.5 text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-muted/40 transition-colors"
              >
                <ChevronLeft size={14} /> Previous
              </button>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="flex items-center gap-1 rounded-lg border px-3 py-1.5 text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-muted/40 transition-colors"
              >
                Next <ChevronRight size={14} />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
