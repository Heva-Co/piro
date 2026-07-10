import { publicApi, type ServiceStatus } from "@/src/lib/api";
import { StatusHeader } from "@/src/components/StatusHeader";
import { ServiceRow } from "@/src/components/ServiceRow";
import { IncidentCard } from "@/src/components/IncidentCard";
import { MaintenanceCard } from "@/src/components/MaintenanceCard";
import { AutoRefresh } from "@/src/components/AutoRefresh";

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

  const activeIncidents = incidents.filter((i) => i.status !== "Resolved");
  const hasActiveIncident = activeIncidents.length > 0;
  const hasGlobalIncident = activeIncidents.some((i) => i.isGlobal);

  // Each service's currentStatus is already computed server-side (ServiceStatusService),
  // factoring in checks + active incidents + maintenance windows — never re-derive it
  // client-side from raw incident impacts, or the two can silently disagree.
  const downCount = services.filter((s) => s.status === "DOWN").length;
  const degradedCount = services.filter((s) => s.status === "DEGRADED").length;
  const totalCount = services.length;
  const majorThreshold = totalCount > 1 ? totalCount / 2 : 1;

  const overallStatus: ServiceStatus = downCount > 0
    ? "DOWN"
    : degradedCount > 0 || hasActiveIncident
      ? "DEGRADED"
      : ongoingMaintenances.length > 0
        ? "MAINTENANCE"
        : totalCount > 0
          ? "UP"
          : "NO_DATA";

  let statusText: string;
  if (hasGlobalIncident) {
    statusText = "Major incident in progress";
  } else if (downCount >= majorThreshold) {
    statusText = "Major system outage";
  } else if (downCount > 1) {
    statusText = "Multiple services disrupted";
  } else if (downCount === 1) {
    statusText = "Service disruption";
  } else if (degradedCount > 1) {
    statusText = "Multiple services degraded";
  } else if (degradedCount === 1) {
    statusText = "Partial service degradation";
  } else if (hasActiveIncident) {
    statusText = "Active incident in progress";
  } else if (ongoingMaintenances.length > 0) {
    statusText = "Under maintenance";
  } else {
    statusText = "All systems operational";
  }

  return (
    <main className="mx-auto w-full max-w-screen-lg px-8 py-10 flex flex-col gap-6">
      <AutoRefresh />
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl sm:text-3xl font-bold">Service Status</h1>
        <p className="text-sm text-muted-foreground">Real-time status of our services</p>
      </div>

      <StatusHeader status={overallStatus} text={statusText} />

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
              <ServiceRow
                service={service}
                overview={overviewBySlug[service.slug] ?? null}
                incidentStatus={service.status}
              />
            </div>
          ))
        )}
      </section>

    </main>
  );
}
