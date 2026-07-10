import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Save } from "lucide-react";
import { siteApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { INCIDENT_CORRELATION_MODE_MAP, type IncidentCorrelationModeKey } from "@/constants/incidents";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";

export default function IncidentsConfigPage() {
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEYS.INCIDENTS_CONFIG,
    queryFn: siteApi.getIncidentsConfig,
  });

  const [correlationMode, setCorrelationMode] = useState<IncidentCorrelationModeKey>("Hybrid");
  const [globalThreshold, setGlobalThreshold] = useState(3);
  const [globalCorrelationWindowMinutes, setGlobalCorrelationWindowMinutes] = useState(5);
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!data) return;
    setCorrelationMode(data.correlationMode as IncidentCorrelationModeKey);
    setGlobalThreshold(data.globalThreshold);
    setGlobalCorrelationWindowMinutes(data.globalCorrelationWindowMinutes);
  }, [data]);

  const mutation = useMutation({
    mutationFn: () =>
      siteApi.updateIncidentsConfig({
        correlationMode,
        globalThreshold,
        globalCorrelationWindowMinutes,
      }),
    onMutate: () => { setSaving(true); setError(""); },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INCIDENTS_CONFIG });
      setSuccess(true);
      setTimeout(() => setSuccess(false), 3000);
    },
    onError: () => setError("Failed to save settings."),
    onSettled: () => setSaving(false),
  });

  if (isLoading) {
    return (
      <>
        <div className="max-w-4xl space-y-4">
          <div className="mb-6">
            <Skeleton className="h-8 w-40 mb-2" />
            <Skeleton className="h-4 w-96" />
          </div>
          <div className="rounded-xl border bg-card p-6 space-y-4">
            <Skeleton className="h-5 w-36" />
            <Skeleton className="h-4 w-72" />
            <Skeleton className="h-9 w-48" />
          </div>
          <div className="rounded-xl border bg-card p-6 space-y-4">
            <Skeleton className="h-5 w-44" />
            <Skeleton className="h-4 w-80" />
            <Skeleton className="h-9 w-56" />
          </div>
        </div>
      </>
    );
  }

  const showGlobalOptions = correlationMode === "Global" || correlationMode === "Hybrid";

  return (
    <>
      <div className="max-w-4xl space-y-4">
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Incidents</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Configure how incidents are automatically created and correlated across services.
          </p>
        </div>

        {error && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            {error}
          </div>
        )}

        {/* ── Correlation ── */}
        <div className="rounded-xl border bg-card p-6">
          <div className="mb-4">
            <h2 className="text-base font-semibold">Incident Correlation</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Controls how alert-triggered incidents are grouped across services.
            </p>
          </div>

          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5 max-w-xs">
              <label className="text-sm font-medium">Correlation mode</label>
              <Select
                value={correlationMode}
                onValueChange={(v) => v && setCorrelationMode(v as IncidentCorrelationModeKey)}
              >
                <SelectTrigger className="w-full">
                  <SelectValue>{(v: IncidentCorrelationModeKey) => INCIDENT_CORRELATION_MODE_MAP[v]?.label ?? v}</SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {(Object.keys(INCIDENT_CORRELATION_MODE_MAP) as IncidentCorrelationModeKey[]).map((key) => (
                    <SelectItem key={key} value={key}>
                      <div className="flex flex-col">
                        <span>{INCIDENT_CORRELATION_MODE_MAP[key].label}</span>
                        <span className="text-xs text-muted-foreground">
                          {INCIDENT_CORRELATION_MODE_MAP[key].description}
                        </span>
                      </div>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {showGlobalOptions && (
              <div className="grid grid-cols-2 gap-4 max-w-lg">
                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-medium">Global threshold</label>
                  <input
                    type="number"
                    min={1}
                    value={globalThreshold}
                    onChange={(e) => setGlobalThreshold(Number(e.target.value))}
                    className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                  />
                  <p className="text-xs text-muted-foreground">
                    Number of services affected before escalating to a global incident.
                  </p>
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-medium">Correlation window (minutes)</label>
                  <input
                    type="number"
                    min={1}
                    value={globalCorrelationWindowMinutes}
                    onChange={(e) => setGlobalCorrelationWindowMinutes(Number(e.target.value))}
                    className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                  />
                  <p className="text-xs text-muted-foreground">
                    Time window in which failures are considered related for global correlation.
                  </p>
                </div>
              </div>
            )}
          </div>
        </div>

        <div className="flex justify-end">
          <button
            onClick={() => mutation.mutate()}
            disabled={saving}
            className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
          >
            <Save size={14} />
            {success ? "Saved!" : saving ? "Saving…" : "Save"}
          </button>
        </div>
      </div>
    </>
  );
}
