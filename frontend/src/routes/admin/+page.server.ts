import { adminApi, publicApi } from "$lib/api";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ locals }) => {
  const token = locals.accessToken!;

  const [services, incidents, maintenances] = await Promise.all([
    adminApi.getServices(token).catch(() => []),
    adminApi.getIncidents(token, false).catch(() => []),
    adminApi.getMaintenances(token).catch(() => []),
  ]);

  return { services, incidents, maintenances };
};
