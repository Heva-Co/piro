import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Search, FileText, Settings } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { useAllChecks } from "@/hooks/useChecks";
import { ROUTES } from "@/constants/routes";

// ── Status pill ───────────────────────────────────────────────────────────────

function StatusPill({ status }: { status: string }) {
  const s = (status ?? "").toLowerCase();
  const color =
    s === "up"       ? "text-green-600 bg-green-50 border-green-200" :
    s === "down"     ? "text-red-600 bg-red-50 border-red-200" :
    s === "degraded" ? "text-yellow-600 bg-yellow-50 border-yellow-200" :
                       "text-muted-foreground bg-muted border-border";

  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-semibold ${color}`}>
      <span className={`size-1.5 rounded-full ${s === "up" ? "bg-green-500" : s === "down" ? "bg-red-500" : s === "degraded" ? "bg-yellow-500" : "bg-muted-foreground"}`} />
      {s.charAt(0).toUpperCase() + s.slice(1)}
    </span>
  );
}

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

// ── Page ──────────────────────────────────────────────────────────────────────

export default function ChecksPage() {
  const navigate = useNavigate();
  const { data: checks, isLoading } = useAllChecks();
  const [search, setSearch] = useState("");

  const filtered = (checks ?? []).filter((c) => {
    const q = search.toLowerCase();
    return (
      c.name.toLowerCase().includes(q) ||
      c.serviceName.toLowerCase().includes(q) ||
      c.type.toLowerCase().includes(q)
    );
  });

  const total    = checks?.length ?? 0;
  const upCount  = checks?.filter((c) => c.currentStatus.toLowerCase() === "up").length ?? 0;
  const degraded = checks?.filter((c) => c.currentStatus.toLowerCase() === "degraded").length ?? 0;
  const down     = checks?.filter((c) => c.currentStatus.toLowerCase() === "down").length ?? 0;

  return (
    <AdminLayout title="Checks">
      <div className="flex flex-col gap-6">
        {/* Header */}
        <div>
          <h1 className="text-xl font-bold">Checks</h1>
          <p className="text-sm text-muted-foreground mt-0.5">All monitoring checks across every service.</p>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-4 gap-4">
          {isLoading ? (
            <>
              <StatCardSkeleton />
              <StatCardSkeleton />
              <StatCardSkeleton />
              <StatCardSkeleton />
            </>
          ) : (
            <>
              <StatCard label="Total"    value={total} />
              <StatCard label="Up"       value={upCount}  color="text-green-600" />
              <StatCard label="Degraded" value={degraded} color="text-yellow-600" />
              <StatCard label="Down"     value={down}     color="text-red-600" />
            </>
          )}
        </div>

        {/* Table card */}
        <div className="rounded-xl border bg-card overflow-hidden">
          {/* Search */}
          <div className="px-4 py-3 border-b">
            <div className="flex items-center gap-2.5 rounded-lg border bg-background px-3 py-2 text-sm">
              <Search size={14} className="text-muted-foreground shrink-0" />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search checks, services, types..."
                className="flex-1 bg-transparent outline-none text-sm"
              />
            </div>
          </div>

          {isLoading ? (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Status</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Check</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Service</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Type</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Cron</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Active</th>
                  <th className="px-5 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {Array.from({ length: 4 }).map((_, i) => (
                  <tr key={i}>
                    <td className="px-5 py-3"><Skeleton className="h-6 w-16 rounded-full" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-36" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-28" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-5 w-14 rounded-full" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-20" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-8" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-12 ml-auto" /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : filtered.length === 0 ? (
            <div className="px-5 py-8 text-sm text-muted-foreground text-center">
              {search ? "No checks match your search." : "No checks configured yet."}
            </div>
          ) : (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Status</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Check</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Service</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Type</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Cron</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Active</th>
                  <th className="px-5 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {filtered.map((check) => (
                  <tr key={`${check.serviceSlug}-${check.slug}`} className="hover:bg-muted/30 transition-colors">
                    <td className="px-5 py-3">
                      <StatusPill status={check.currentStatus} />
                    </td>
                    <td className="px-5 py-3 font-semibold">{check.name}</td>
                    <td className="px-5 py-3 text-muted-foreground">{check.serviceName}</td>
                    <td className="px-5 py-3">
                      <span className="rounded-full border px-2 py-0.5 text-xs font-medium">
                        {check.type.toUpperCase()}
                      </span>
                    </td>
                    <td className="px-5 py-3 font-mono text-xs text-muted-foreground">{check.cron}</td>
                    <td className={`px-5 py-3 text-sm font-medium ${check.isActive ? "text-green-600" : "text-muted-foreground"}`}>
                      {check.isActive ? "Yes" : "No"}
                    </td>
                    <td className="px-5 py-3">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          onClick={() => navigate(ROUTES.CHECKS.LOGS(check.serviceSlug, check.slug))}
                          title="View logs"
                          className="text-muted-foreground hover:text-foreground transition-colors"
                        >
                          <FileText size={16} />
                        </button>
                        <button
                          onClick={() => navigate(ROUTES.CHECKS.DETAIL(check.serviceSlug, check.slug))}
                          title="Configure"
                          className="text-muted-foreground hover:text-foreground transition-colors"
                        >
                          <Settings size={16} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </AdminLayout>
  );
}
