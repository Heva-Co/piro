import { useQuery } from "@tanstack/react-query";
import { dashboardApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import MetricStatTile from "@/features/dashboard/components/MetricStatTile";
import MetricStatTileSkeleton from "@/features/dashboard/components/MetricStatTileSkeleton";
import ChartCardSkeleton from "@/features/dashboard/components/ChartCardSkeleton";
import VolumeLineChart from "@/features/dashboard/components/VolumeLineChart";
import ByServiceBarChart from "@/features/dashboard/components/ByServiceBarChart";
import { formatDuration, formatPercent, formatMonthRange, currentMonthRange } from "@/features/dashboard/utils";

export function MetricsSection() {
  const { from, to } = currentMonthRange();

  const { data, isLoading, isError } = useQuery({
    queryKey: QUERY_KEYS.DASHBOARD_METRICS(from, to),
    queryFn: () => dashboardApi.metrics(from, to),
  });

  if (isLoading) {
    return (
      <div className="mb-6 flex flex-col gap-6">
        <div>
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-semibold text-foreground">Incidents — This Month</h2>
          </div>
          <div className="grid grid-cols-2 lg:grid-cols-3 gap-4 mb-4">
            <MetricStatTileSkeleton label="MTTA" />
            <MetricStatTileSkeleton label="MTTR" />
            <MetricStatTileSkeleton label="Incidents" />
          </div>
          <div className="flex flex-col lg:flex-row gap-4">
            <ChartCardSkeleton title="Incident volume" />
            <ChartCardSkeleton title="Incidents by service" />
          </div>
        </div>
        <div>
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-semibold text-foreground">Alerts — This Month</h2>
          </div>
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
            <MetricStatTileSkeleton label="MTTA" />
            <MetricStatTileSkeleton label="MTTR" />
            <MetricStatTileSkeleton label="Time to Incident" />
            <MetricStatTileSkeleton label="→ Incident Rate" />
          </div>
          <div className="flex flex-col lg:flex-row gap-4">
            <ChartCardSkeleton title="Alert volume" />
            <ChartCardSkeleton title="Alerts by service" />
          </div>
        </div>
      </div>
    );
  }

  if (isError || !data) {
    return <div className="text-sm text-destructive mb-6">Failed to load metrics.</div>;
  }

  const { incidentMetrics, alertMetrics } = data;

  const dailyIncidentData = data.dailyIncidentCounts.map((d) => ({ date: d.date.slice(5), count: d.count }));
  const incidentServiceData = data.incidentsByService.slice(0, 8);

  const dailyAlertData = alertMetrics.dailyAlertCounts.map((d) => ({ date: d.date.slice(5), count: d.count }));
  const alertServiceData = alertMetrics.alertsByService.slice(0, 8);

  return (
    <div className="mb-6 flex flex-col gap-6">
      {/* ── Incidents ── */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-semibold text-foreground">Incidents — This Month</h2>
          <span className="text-xs text-muted-foreground/70">{formatMonthRange(data.from, data.to)}</span>
        </div>

        <div className="grid grid-cols-2 lg:grid-cols-3 gap-4 mb-4">
          <MetricStatTile label="MTTA" value={formatDuration(incidentMetrics.mttaSeconds)} hint="Mean time to acknowledge an incident" />
          <MetricStatTile label="MTTR" value={formatDuration(incidentMetrics.mttrSeconds)} hint="Mean time to resolve an incident" />
          <MetricStatTile label="Incidents" value={String(incidentMetrics.incidentCount)} hint="Declared this period" />
        </div>

        <div className="flex flex-col lg:flex-row gap-4">
          <VolumeLineChart
            title="Incident volume"
            data={dailyIncidentData}
            emptyLabel="No incidents this month."
            seriesName="Incidents"
          />
          <ByServiceBarChart
            title="Incidents by service"
            data={incidentServiceData}
            emptyLabel="No incidents this month."
            seriesName="Incidents"
          />
        </div>
      </div>

      {/* ── Alerts ── */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-semibold text-foreground">Alerts — This Month</h2>
        </div>

        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
          <MetricStatTile label="MTTA" value={formatDuration(alertMetrics.mttaSeconds)} hint="Mean time to acknowledge an alert" />
          <MetricStatTile label="MTTR" value={formatDuration(alertMetrics.mttrSeconds)} hint="Mean time to resolve an alert" />
          <MetricStatTile
            label="Time to Incident"
            value={formatDuration(alertMetrics.meanTimeToIncidentSeconds)}
            hint="Mean time before a human links an alert to an incident"
          />
          <MetricStatTile
            label="→ Incident Rate"
            value={formatPercent(alertMetrics.alertToIncidentConversionRate)}
            hint={`${alertMetrics.alertCount} alerts → ${incidentMetrics.incidentCount} incidents`}
          />
        </div>

        <div className="flex flex-col lg:flex-row gap-4">
          <VolumeLineChart
            title="Alert volume"
            data={dailyAlertData}
            emptyLabel="No alerts this month."
            seriesName="Alerts"
          />
          <ByServiceBarChart
            title="Alerts by service"
            data={alertServiceData}
            emptyLabel="No alerts this month."
            seriesName="Alerts"
          />
        </div>
      </div>
    </div>
  );
}
