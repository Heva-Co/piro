import { error } from "@sveltejs/kit";
import { publicApi, ApiError } from "$lib/api";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ params }) => {
  const { slug } = params;

  try {
    const now = Math.floor(Date.now() / 1000);
    const todayStart = now - (now % 86400); // start of current UTC day

    const service = await publicApi.getService(slug);
    const [overview, todayHistory, incidents, maintenances] = await Promise.all([
      publicApi.getOverview(slug, service.historyDaysDesktop),
      publicApi.getHistory(slug, todayStart, now).catch(() => []),
      publicApi.getIncidents(true).catch(() => []),
      publicApi.getMaintenances().catch(() => []),
    ]);

    const serviceIncidents = incidents.filter((i) =>
      i.services.some((s) => s.serviceSlug === slug)
    );
    const serviceMaintenances = maintenances.filter((m) =>
      m.serviceSlugs.includes(slug)
    );

    return { service, overview, todayHistory, serviceIncidents, serviceMaintenances, };
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) {
      error(404, "Service not found");
    }
    throw e;
  }
};
