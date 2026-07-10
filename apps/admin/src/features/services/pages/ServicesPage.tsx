import { useNavigate } from "react-router-dom";
import { Filter, Plus, Settings } from "lucide-react";
import { StatusPill } from "@/components/StatusBadge";
import { useServices } from "@/hooks/useServices";
import { ROUTES } from "@/constants/routes";

function initials(name: string) {
  const words = name.trim().split(/\s+/);
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[1][0]).toUpperCase();
}

export default function ServicesPage() {
  const navigate = useNavigate();
  const { data: services, isLoading, isError } = useServices();

  return (
    <>
      <div>
        <div className="flex items-center justify-between mb-4">
          <h1 className="text-2xl font-bold">Services</h1>
          <button
            onClick={() => navigate(ROUTES.SERVICES.NEW)}
            className="flex items-center gap-1.5 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <Plus size={14} /> New Service
          </button>
        </div>

        <div className="flex items-center mb-3">
          <button className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors">
            <Filter size={13} /> Filters
          </button>
        </div>

        <div className="rounded-xl border bg-card overflow-hidden">
          {isLoading ? (
            <div className="px-6 py-8 text-sm text-muted-foreground">Loading…</div>
          ) : isError ? (
            <div className="px-6 py-8 text-sm text-destructive">Failed to load services.</div>
          ) : !services || services.length === 0 ? (
            <div className="flex flex-col items-center justify-center gap-4 py-20">
              <img src="/piro.svg" alt="Piro" className="h-16 w-16 opacity-20" />
              <div className="text-center">
                <p className="text-sm font-medium text-muted-foreground">No services yet</p>
                <p className="text-xs text-muted-foreground mt-1">
                  Add your first service to start monitoring uptime.
                </p>
              </div>
              <button
                onClick={() => navigate(ROUTES.SERVICES.NEW)}
                className="flex items-center gap-1.5 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
              >
                <Plus size={14} /> New Service
              </button>
            </div>
          ) : (
            <>
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/40">
                    <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Service</th>
                    <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Slug</th>
                    <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Status</th>
                    <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Hidden</th>
                    <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Checks</th>
                    <th className="px-5 py-3" />
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {services.map((service) => (
                    <tr key={service.slug} className="hover:bg-muted/30 transition-colors">
                      <td className="px-5 py-3">
                        <div className="flex items-center gap-3">
                          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold text-muted-foreground">
                            {initials(service.name)}
                          </div>
                          <span className="font-medium">{service.name}</span>
                        </div>
                      </td>
                      <td className="px-5 py-3">
                        <code className="rounded border bg-muted px-2 py-0.5 text-xs font-mono">
                          {service.slug}
                        </code>
                      </td>
                      <td className="px-5 py-3">
                        <StatusPill status={service.currentStatus} />
                      </td>
                      <td className="px-5 py-3 text-sm text-muted-foreground">
                        {service.isHidden ? "YES" : "NO"}
                      </td>
                      <td className="px-5 py-3 text-sm text-muted-foreground">{service.checkCount ?? '—'}</td>
                      <td className="px-5 py-3 text-right">
                        <button
                          onClick={() => navigate(ROUTES.SERVICES.DETAIL(service.slug))}
                          className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors"
                        >
                          <Settings size={13} /> Configure
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="px-5 py-2.5 border-t bg-muted/20 text-xs text-muted-foreground">
                Showing 1–{services.length} of {services.length} service{services.length !== 1 ? "s" : ""}
              </div>
            </>
          )}
        </div>
      </div>
    </>
  );
}
