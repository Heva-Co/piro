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
          <MetricStatTile
            label="MTTA"
            value={formatDuration(incidentMetrics.mttaSeconds)}
            hint="Mean time to acknowledge an incident"
            info="Mean time from an incident's start to when it was acknowledged, averaged over incidents started this month that have been acknowledged. Blank if none have been acknowledged yet."
          />
          <MetricStatTile
            label="MTTR"
            value={formatDuration(incidentMetrics.mttrSeconds)}
            hint="Mean time to resolve an incident"
            info="Mean time from an incident's start to its end, averaged over incidents started this month that have been resolved. Blank if none have been resolved yet."
          />
          <MetricStatTile
            label="Incidents"
            value={String(incidentMetrics.incidentCount)}
            hint="Declared this period"
            info="Count of incidents whose start date falls within this month."
          />
        </div>

        <div className="flex flex-col lg:flex-row gap-4">
          <VolumeLineChart
            title="Incident volume"
            data={dailyIncidentData}
            emptyLabel="No incidents this month."
            seriesName="Incidents"
            info="Number of incidents started on each day this month, grouped by their start date (UTC)."
          />
          <ByServiceBarChart
            title="Incidents by service"
            data={incidentServiceData}
            emptyLabel="No incidents this month."
            seriesName="Incidents"
            info="Incidents started this month, counted per affected service. An incident affecting several services is counted once for each. Top 8 services shown."
          />
        </div>
      </div>

      {/* ── Alerts ── */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-semibold text-foreground">Alerts — This Month</h2>
        </div>

        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
          <MetricStatTile
            label="MTTA"
            value={formatDuration(alertMetrics.mttaSeconds)}
            hint="Mean time to acknowledge an alert"
            info="Mean time from an alert firing to when it was acknowledged, averaged over alerts fired this month that have been acknowledged. Blank if none have been acknowledged yet."
          />
          <MetricStatTile
            label="MTTR"
            value={formatDuration(alertMetrics.mttrSeconds)}
            hint="Mean time to resolve an alert"
            info="Mean time from an alert firing to when it auto-resolved, averaged over alerts fired this month that have resolved. Blank if none have resolved yet."
          />
          <MetricStatTile
            label="Time to Incident"
            value={formatDuration(alertMetrics.meanTimeToIncidentSeconds)}
            hint="Mean time before a human links an alert to an incident"
            info="Mean time from an alert firing to the creation of the incident a human linked it to, over alerts linked this month. Measures how long an alert sat before someone declared an incident — not automatic detection. Blank if none were linked."
          />
          <MetricStatTile
            label="→ Incident Rate"
            value={formatPercent(alertMetrics.alertToIncidentConversionRate)}
            hint={`${alertMetrics.alertCount} alerts → ${incidentMetrics.incidentCount} incidents`}
            info="Share of this month's alerts that were ever linked to an incident (alerts linked ÷ alerts fired). Reflects what fraction a human judged incident-worthy."
          />
        </div>

        <div className="flex flex-col lg:flex-row gap-4">
          <VolumeLineChart
            title="Alert volume"
            data={dailyAlertData}
            emptyLabel="No alerts this month."
            seriesName="Alerts"
            info="Number of distinct alerts that fired on each day this month, grouped by fire date (UTC). Counts each alert once — repeated failures folded into one alert (its occurrence count) do not inflate this."
          />
          <ByServiceBarChart
            title="Alerts by service"
            data={alertServiceData}
            emptyLabel="No alerts this month."
            seriesName="Alerts"
            info="Alerts fired this month, counted per service. External/orphan alerts with no associated service are excluded. Top 8 services shown."
          />
        </div>
      </div>
    </div>
  );
}
