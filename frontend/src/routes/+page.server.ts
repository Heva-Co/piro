import { redirect } from "@sveltejs/kit";
import { publicApi, authApi, ApiError, type ServiceStatus } from "$lib/api";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async () => {
  // Check if setup is required — redirect to /setup if so
  try {
    const status = await authApi.setupStatus();
    if (status.setupRequired) {
      redirect(302, "/setup");
    }
  } catch (e) {
    if (e instanceof ApiError) {
      // API unreachable — still render the page (will show empty state)
    } else {
      throw e;
    }
  }

  const [services, incidents, maintenances] = await Promise.all([
    publicApi.getServices().catch(() => []),
    publicApi.getIncidents(false).catch(() => []),
    publicApi.getMaintenances().catch(() => []),
  ]);

  // Fetch per-service overview (uptime + bar data) in parallel
  const overviews = await Promise.all(
    services.map((s) =>
      publicApi.getOverview(s.slug, s.historyDaysDesktop).catch(() => null)
    )
  );
  const overviewBySlug = Object.fromEntries(
    services.map((s, i) => [s.slug, overviews[i]])
  );

  const now = Math.floor(Date.now() / 1000);
  const ongoingMaintenances = maintenances.filter(
    (m) => m.status === "Active" && m.upcomingEvents.some((e) => e.status === "Ongoing")
  );
  const upcomingMaintenances = maintenances.filter(
    (m) =>
      m.status === "Active" &&
      m.upcomingEvents.some((e) => e.status === "Scheduled" && e.startDateTime - now < 86400)
  );

  // Overall page status
  // Active incidents degrade the banner to at least DEGRADED ("Active incident in progress").
  // Service-level DOWN still wins over incident-only DEGRADED.
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

  const statusText: Record<string, string> = {
    UP: "All systems operational",
    DEGRADED: hasActiveIncident && !servicesDegraded ? "Active incident in progress" : "Partial system outage",
    DOWN: "Major system outage",
    MAINTENANCE: "Under maintenance",
    NO_DATA: "No status data",
  };

  return {
    services,
    overviewBySlug,
    incidents,
    ongoingMaintenances,
    upcomingMaintenances,
    overallStatus,
    overallStatusText: statusText[overallStatus],
  };
};
