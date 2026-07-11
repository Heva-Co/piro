import { useQuery } from "@tanstack/react-query";
import { BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { dashboardApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { Skeleton } from "@/components/ui/skeleton";

function formatDuration(seconds: number | null): string {
  if (seconds == null) return "—";
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const minutes = seconds / 60;
  if (minutes < 60) return `${minutes.toFixed(1)}m`;
  const hours = minutes / 60;
  if (hours < 24) return `${hours.toFixed(1)}h`;
  return `${(hours / 24).toFixed(1)}d`;
}

function formatPercent(ratio: number | null): string {
  if (ratio == null) return "—";
  return `${Math.round(ratio * 100)}%`;
}

/** Formats an ISO date range as "Jul 1 – Aug 1", the API's `to` being exclusive. */
function formatRange(fromIso: string, toIso: string): string {
  const fmt = (iso: string) =>
    new Date(`${iso}T00:00:00Z`).toLocaleDateString("en-US", { month: "short", day: "numeric", timeZone: "UTC" });
  return `${fmt(fromIso)} – ${fmt(toIso)}`;
}

function StatTile({ label, value, hint }: { label: string; value: string; hint: string }) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5 shadow-sm">
      <p className="text-sm text-gray-500 mb-1">{label}</p>
      <p className="text-3xl font-bold text-gray-900">{value}</p>
      <p className="text-xs text-gray-400 mt-1">{hint}</p>
    </div>
  );
}

function StatTileSkeleton({ label }: { label: string }) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5 shadow-sm">
      <p className="text-sm text-gray-500 mb-1">{label}</p>
      <Skeleton className="h-9 w-16 mb-1" />
      <Skeleton className="h-3 w-24" />
    </div>
  );
}

function ChartSkeleton({ title }: { title: string }) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-5">
      <h3 className="text-sm font-medium text-gray-700 mb-3">{title}</h3>
      <Skeleton className="h-55 w-full" />
    </div>
  );
}

function currentMonthRange() {
  const now = new Date();
  const from = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
  const to = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  return { from: iso(from), to: iso(to) };
}

export function MetricsSection() {
  const { from, to } = currentMonthRange();

  const { data, isLoading, isError } = useQuery({
    queryKey: QUERY_KEYS.DASHBOARD_METRICS(from, to),
    queryFn: () => dashboardApi.metrics(from, to),
  });

  if (isLoading) {
    return (
      <div className="mb-6">
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-semibold text-gray-900">This Month</h2>
        </div>
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
          <StatTileSkeleton label="MTTA" />
          <StatTileSkeleton label="MTTR" />
          <StatTileSkeleton label="MTTD" />
          <StatTileSkeleton label="Signal Ratio" />
        </div>
        <div className="flex flex-col lg:flex-row gap-4">
          <div className="flex-1 lg:w-2/3">
            <ChartSkeleton title="Incident volume" />
          </div>
          <div className="lg:w-1/3">
            <ChartSkeleton title="Incidents by service" />
          </div>
        </div>
      </div>
    );
  }
  if (isError || !data) {
    return <div className="text-sm text-red-500 mb-6">Failed to load metrics.</div>;
  }

  const dailyData = data.dailyIncidentCounts.map((d) => ({ date: d.date.slice(5), count: d.count }));
  const serviceData = data.incidentsByService.slice(0, 8);

  return (
    <div className="mb-6">
      <div className="flex items-center justify-between mb-3">
        <h2 className="font-semibold text-gray-900">This Month</h2>
        <span className="text-xs text-gray-400">{formatRange(data.from, data.to)}</span>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
        <StatTile label="MTTA" value={formatDuration(data.mttaSeconds)} hint="Mean time to acknowledge" />
        <StatTile label="MTTR" value={formatDuration(data.mttrSeconds)} hint="Mean time to resolve" />
        <StatTile label="MTTD" value={formatDuration(data.mttdSeconds)} hint="Mean time alert → incident" />
        <StatTile label="Signal Ratio" value={formatPercent(data.alertNoiseRatio)} hint={`${data.alertCount} alerts → ${data.incidentCount} incidents`} />
      </div>

      <div className="flex flex-col lg:flex-row gap-4">
        <div className="flex-1 lg:w-2/3 bg-white rounded-lg border border-gray-200 shadow-sm p-5">
          <h3 className="text-sm font-medium text-gray-700 mb-3">Incident volume</h3>
          {dailyData.length === 0 ? (
            <p className="text-sm text-gray-400 py-8 text-center">No incidents this month.</p>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={dailyData} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--color-border)" />
                <XAxis dataKey="date" tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }} axisLine={false} tickLine={false} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }} axisLine={false} tickLine={false} width={28} />
                <Tooltip
                  contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--color-border)" }}
                  labelStyle={{ color: "var(--color-foreground)" }}
                />
                <Line type="monotone" dataKey="count" name="Incidents" stroke="var(--color-chart-1)" strokeWidth={2} dot={{ r: 3 }} />
              </LineChart>
            </ResponsiveContainer>
          )}
        </div>

        <div className="lg:w-1/3 bg-white rounded-lg border border-gray-200 shadow-sm p-5">
          <h3 className="text-sm font-medium text-gray-700 mb-3">Incidents by service</h3>
          {serviceData.length === 0 ? (
            <p className="text-sm text-gray-400 py-8 text-center">No incidents this month.</p>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={serviceData} layout="vertical" margin={{ top: 4, right: 16, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="var(--color-border)" />
                <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }} axisLine={false} tickLine={false} />
                <YAxis
                  type="category"
                  dataKey="serviceName"
                  tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }}
                  axisLine={false}
                  tickLine={false}
                  width={90}
                />
                <Tooltip
                  contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--color-border)" }}
                  labelStyle={{ color: "var(--color-foreground)" }}
                />
                <Bar dataKey="count" name="Incidents" fill="var(--color-chart-2)" radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>
    </div>
  );
}
