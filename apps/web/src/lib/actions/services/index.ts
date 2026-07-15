import { get } from "@/src/lib/http";
import type { components } from "@/src/lib/api-types";

export type ServiceStatus = components["schemas"]["ServiceStatus"];
export type PublicService = components["schemas"]["PublicServiceDto"];
export type DailyStatsDto = components["schemas"]["DailyStatsDto"];
export type ServiceOverviewDto = components["schemas"]["ServiceOverviewDto"];

export const servicesApi = {
  list: () => get<PublicService[]>("/public/services"),

  get: (slug: string) => get<PublicService>(`/public/services/${slug}`),

  overview: (slug: string, days: number) =>
    get<ServiceOverviewDto>(`/public/services/${slug}/overview?days=${days}`),
};
