import { useQuery } from "@tanstack/react-query";
import { maintenancesApi, workersApi } from "@/lib/api";
import { useAllServices } from "@/hooks/useServices";
import { incidentsApi } from "@/lib/actions/incidents";
import { QUERY_KEYS } from "@/constants/api";
import { PageHeader } from "@/components/PageHeader";
import { MetricsSection } from "@/features/dashboard/components/MetricsSection";
import DashboardStatCard from "@/features/dashboard/components/DashboardStatCard";
import ServicesTable from "@/features/dashboard/components/ServicesTable";
import ActiveIncidentsCard from "@/features/dashboard/components/ActiveIncidentsCard";
import ActiveMaintenancesCard from "@/features/dashboard/components/ActiveMaintenancesCard";
import NoLocalExecutionBanner from "@/features/dashboard/components/NoLocalExecutionBanner";
import { ShowcaseOverlay } from "@/features/showcase/components/ShowcaseOverlay";
import { useShowcase } from "@/features/showcase/hooks/useShowcase";

export default function DashboardPage() {
  const { shouldShow: showShowcase, dismiss: dismissShowcase } = useShowcase();
  const servicesQuery = useAllServices();
  const incidentsQuery = useQuery({
    queryKey: QUERY_KEYS.INCIDENTS,
    queryFn: () => incidentsApi.list(),
  });
  const maintenancesQuery = useQuery({
    queryKey: QUERY_KEYS.MAINTENANCES,
    queryFn: maintenancesApi.list,
  });
  const workersQuery = useQuery({
    queryKey: QUERY_KEYS.WORKERS,
    queryFn: workersApi.list,
    refetchInterval: 30_000,
  });

  const services = servicesQuery.data ?? [];
  const incidents = incidentsQuery.data ?? [];
  const maintenances = maintenancesQuery.data ?? [];

  const totalServices = services.length;
  const operational = services.filter((s) => s.currentStatus === "UP").length;
  const withIssues = services.filter(
    (s) => s.currentStatus === "DOWN" || s.currentStatus === "DEGRADED"
  ).length;
  const activeIncidentsCount = incidents.filter((i) => i.status !== "Resolved").length;
  const activeMaintenances = maintenances.filter(
    (m) => m.displayStatus === "Active" || m.displayStatus === "Scheduled"
  );

  const workers = workersQuery.data ?? [];
  const noLocalExecution = !workersQuery.isLoading && !workers.some((w) => w.isConnected);

  return (
    <>
      {showShowcase && <ShowcaseOverlay onClose={dismissShowcase} />}

      <PageHeader breadcrumbs={[{ label: "Dashboard" }]} />

      {noLocalExecution && <NoLocalExecutionBanner />}

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <DashboardStatCard label="Total Services" value={totalServices} color="text-foreground" isLoading={servicesQuery.isLoading} />
        <DashboardStatCard label="Operational" value={operational} color="text-green-600" isLoading={servicesQuery.isLoading} />
        <DashboardStatCard label="With Issues" value={withIssues} color="text-red-600" isLoading={servicesQuery.isLoading} />
        <DashboardStatCard label="Active Incidents" value={activeIncidentsCount} color="text-amber-600" isLoading={incidentsQuery.isLoading} />
      </div>

      <MetricsSection />

      <div className="flex flex-col lg:flex-row gap-6">
        <ServicesTable services={services} isLoading={servicesQuery.isLoading} isError={servicesQuery.isError} />

        <div className="lg:w-1/3 flex flex-col gap-4">
          <ActiveIncidentsCard incidents={incidents} isLoading={incidentsQuery.isLoading} />
          <ActiveMaintenancesCard maintenances={activeMaintenances} isLoading={maintenancesQuery.isLoading} />
        </div>
      </div>
    </>
  );
}
