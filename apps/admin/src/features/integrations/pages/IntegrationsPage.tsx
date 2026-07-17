import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, Plug } from "lucide-react";
import { Icon } from "@iconify/react";
import { integrationsApi, integrationTypesApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

function Skeleton({ className }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-muted ${className ?? ""}`} />;
}

export default function IntegrationsPage() {
  const navigate = useNavigate();

  const { data: integrations = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });
  const { data: types = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_TYPES,
    queryFn: integrationTypesApi.list,
  });

  return (
    <>
      <div className="flex flex-col gap-6">
        {/* Header */}
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-xl font-bold">Integrations</h1>
            <p className="text-sm text-muted-foreground mt-0.5">
              Manage shared provider credentials used by checks.
            </p>
          </div>
          <button
            onClick={() => navigate(ROUTES.INTEGRATIONS.NEW)}
            className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <Plus size={14} /> New Integration
          </button>
        </div>

        {/* Table / empty state */}
        <div className="rounded-xl border bg-card overflow-hidden">
          {isLoading ? (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/40">
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Name</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Type</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Checks</th>
                  <th className="px-5 py-2.5" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {Array.from({ length: 3 }).map((_, i) => (
                  <tr key={i}>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-40" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-5 w-24 rounded-full" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-8" /></td>
                    <td className="px-5 py-3"><Skeleton className="h-4 w-20 ml-auto" /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : integrations.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 gap-4">
              <Plug size={32} className="text-muted-foreground/40" />
              <p className="text-sm text-muted-foreground">No integrations yet.</p>
              <button
                onClick={() => navigate(ROUTES.INTEGRATIONS.NEW)}
                className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
              >
                <Plus size={14} /> Add your first integration
              </button>
            </div>
          ) : (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/40">
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Name</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Type</th>
                  <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Checks</th>
                  <th className="px-5 py-2.5" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {integrations.map((integration) => {
                  const typeMeta = types.find((t) => t.type === integration.type);
                  return (
                  <tr key={integration.id} className="hover:bg-muted/30 transition-colors">
                    <td className="px-5 py-3 font-medium">
                      <div>{integration.name}</div>
                      {integration.description && (
                        <div className="text-xs text-muted-foreground mt-0.5">{integration.description}</div>
                      )}
                    </td>
                    <td className="px-5 py-3">
                      <span className="inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium">
                        {typeMeta?.iconifyIcon && <Icon icon={typeMeta.iconifyIcon} className="size-3.5" />}
                        {typeMeta?.label ?? integration.type}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-muted-foreground">
                      {integration.checkCount}
                    </td>
                    <td className="px-5 py-3 text-right">
                      <button
                        onClick={() => navigate(ROUTES.INTEGRATIONS.DETAIL(integration.id))}
                        className="rounded-lg border px-3 py-1 text-sm font-medium hover:bg-muted transition-colors"
                      >
                        Configure
                      </button>
                    </td>
                  </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  );
}
