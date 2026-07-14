import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type AlertFor = components["schemas"]["AlertFor"];
export type AlertSeverity = components["schemas"]["AlertSeverity"];
export type AlertConfig = components["schemas"]["AlertConfigDto"];
export type CreateAlertConfigRequest = components["schemas"]["CreateAlertConfigRequest"];
export type UpdateAlertConfigRequest = components["schemas"]["UpdateAlertConfigRequest"];

export const alertConfigsApi = {
  list: (serviceSlug: string, checkSlug: string) =>
    api.get<AlertConfig[]>(ENDPOINTS.ALERT_CONFIGS(serviceSlug, checkSlug)).then((r) => r.data),

  create: (serviceSlug: string, checkSlug: string, data: CreateAlertConfigRequest) =>
    api.post<AlertConfig>(ENDPOINTS.ALERT_CONFIGS(serviceSlug, checkSlug), data).then((r) => r.data),

  update: (
    serviceSlug: string,
    checkSlug: string,
    id: number | string,
    data: Partial<UpdateAlertConfigRequest>
  ) =>
    api.put<AlertConfig>(ENDPOINTS.ALERT_CONFIG(serviceSlug, checkSlug, id), data).then((r) => r.data),

  delete: (serviceSlug: string, checkSlug: string, id: number | string) =>
    api.delete(ENDPOINTS.ALERT_CONFIG(serviceSlug, checkSlug, id)),
};
