import { publicApi, type ServiceStatus } from "@/lib/api";
import { StatusHeader } from "@/components/StatusHeader";
import { ServiceRow } from "@/components/ServiceRow";
import { IncidentCard } from "@/components/IncidentCard";
import { MaintenanceCard } from "@/components/MaintenanceCard";

export const revalidate = 30;

export default async function StatusPage() {
  const [services, incidents, maintenances] = await Promise.all([
    publicApi.services().catch(() => []),
    publicApi.incidents(false).catch(() => []),
    publicApi.maintenances().catch(() => []),
  ]);

  // Fetch per-service overview in parallel
  const overviews = await Promise.all(
    services.map((s) =>
      publicApi.overview(s.slug, s.historyDaysDesktop).catch(() => null)
    )
  );
  const overviewBySlug = Object.fromEntries(services.map((s, i) => [s.slug, overviews[i]]));

  const now = Math.floor(Date.now() / 1000);
  const ongoingMaintenances = maintenances.filter(
    (m) => m.status === "Active" && m.upcomingEvents.some((e) => e.status === "Ongoing")
  );
  const upcomingMaintenances = maintenances.filter(
    (m) =>
      m.status === "Active" &&
      m.upcomingEvents.some(
        (e) => e.status === "Scheduled" && e.startDateTime - now < 86400
      )
  );

  const activeIncidents = incidents.filter((i) => i.state !== "Resolved");
  const hasActiveIncident = activeIncidents.length > 0;
  const allStatuses = services.map((s) => s.status);
  const servicesDown = allStatuses.includes("DOWN");
  const servicesDegraded = allStatuses.includes("DEGRADED");

  const overallStatus: ServiceStatus = servicesDown
    ? "DOWN"
    : servicesDegraded || hasActiveIncident
      ? "DEGRADED"
      : allStatuses.includes("MAINTENANCE") || ongoingMaintenances.length > 0
        ? "MAINTENANCE"
        : allStatuses.length > 0
          ? "UP"
          : "NO_DATA";

  const statusText: Record<ServiceStatus, string> = {
    UP: "All systems operational",
    DEGRADED:
      hasActiveIncident && !servicesDegraded
        ? "Active incident in progress"
        : "Partial system outage",
    DOWN: "Major system outage",
    MAINTENANCE: "Under maintenance",
    NO_DATA: "No status data",
    FAILURE: "No status data",
  };

  return (
    <main className="mx-auto w-full max-w-screen-lg px-8 py-10 flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl sm:text-3xl font-bold">Service Status</h1>
        <p className="text-sm text-muted-foreground">Real-time status of our services</p>
      </div>

      <StatusHeader status={overallStatus} text={statusText[overallStatus]} />

      {activeIncidents.length > 0 && (
        <section className="flex flex-col gap-3">
          <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">
            Active Incidents
          </h2>
          {activeIncidents.map((incident) => (
            <IncidentCard key={incident.id} incident={incident} />
          ))}
        </section>
      )}

      {ongoingMaintenances.length > 0 && (
        <section className="flex flex-col gap-3">
          <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">
            Ongoing Maintenance
          </h2>
          {ongoingMaintenances.map((m) => (
            <MaintenanceCard key={m.id} maintenance={m} />
          ))}
        </section>
      )}

      {upcomingMaintenances.length > 0 && (
        <section className="flex flex-col gap-3">
          <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">
            Upcoming Maintenance
          </h2>
          {upcomingMaintenances.map((m) => (
            <MaintenanceCard key={m.id} maintenance={m} upcoming />
          ))}
        </section>
      )}

      <section className="flex flex-col gap-3">
        {services.length === 0 ? (
          <div className="rounded-2xl border p-8 text-center text-muted-foreground text-sm">
            No services configured yet.
          </div>
        ) : (
          services.map((service) => (
            <div key={service.slug} className="rounded-2xl border overflow-hidden">
              <ServiceRow service={service} overview={overviewBySlug[service.slug] ?? null} />
            </div>
          ))
        )}
      </section>

    </main>
  );
}
