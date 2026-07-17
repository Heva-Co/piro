import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";
import type { Incident } from "@/lib/actions/incidents";

export type AlertSummary = components["schemas"]["AlertSummaryDto"];
export type AlertDetail = components["schemas"]["AlertDetailDto"];
export type AlertPage = components["schemas"]["AlertPageDto"];
export type EscalationDeliveryLog = components["schemas"]["EscalationDeliveryLogDto"];
export type AlertRetentionResult = components["schemas"]["AlertRetentionResultDto"];

export const alertsApi = {
  list: (params?: { page?: number; pageSize?: number; from?: string; to?: string; activeOnly?: boolean }) =>
    api.get<AlertPage>(ENDPOINTS.ALERTS, { params }).then((r) => r.data),

  get: (id: number | string) =>
    api.get<AlertDetail>(ENDPOINTS.ALERT(id)).then((r) => r.data),

  getOpenIncidents: () =>
    api.get<Incident[]>(ENDPOINTS.ALERTS_OPEN_INCIDENTS).then((r) => r.data),

  linkToIncident: (id: number | string, incidentId?: number, serviceIds?: number[]) =>
    api.post<AlertDetail>(ENDPOINTS.ALERT_INCIDENT(id), { incidentId, serviceIds }).then((r) => r.data),

  acknowledge: (id: number | string) =>
    api.post<AlertDetail>(ENDPOINTS.ALERT_ACKNOWLEDGE(id)).then((r) => r.data),

  getEscalationLogs: (id: number | string) =>
    api.get<EscalationDeliveryLog[]>(ENDPOINTS.ALERT_ESCALATION_LOGS(id)).then((r) => r.data),

  // Data retention: preview how many resolved (non-incident-linked) alerts a cutoff would delete…
  previewRetention: (resolvedBefore: string) =>
    api
      .get<AlertRetentionResult>(ENDPOINTS.ALERTS_RETENTION_PREVIEW, { params: { resolvedBefore } })
      .then((r) => r.data),

  // …and permanently delete them.
  deleteByRetention: (resolvedBefore: string) =>
    api
      .post<AlertRetentionResult>(ENDPOINTS.ALERTS_RETENTION_DELETE, { resolvedBefore })
      .then((r) => r.data),
};
