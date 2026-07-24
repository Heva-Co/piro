import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type Check = components["schemas"]["CheckDto"];
export type CheckSummary = components["schemas"]["CheckSummaryDto"];
export type CheckDataPoint = components["schemas"]["CheckDataPointDto"];
export type CheckDailyStats = components["schemas"]["CheckDailyStatsDto"];
export type CreateCheckRequest = components["schemas"]["CreateCheckRequest"];
export type UpdateCheckRequest = components["schemas"]["UpdateCheckRequest"];

/** Full per-CheckType manifest (RFC 0011): display metadata, min interval, allowed alert-fors, and the reflected config schema. */
export type CheckTypeMeta = components["schemas"]["CheckTypeMetaDto"];
/** A single config field's schema — shared with integrations (see lib/actions/integrations). */
export type ConfigFieldSchema = components["schemas"]["ConfigFieldSchemaDto"];
export type ScriptTestResult = components["schemas"]["ScriptTestResultDto"];
export type CheckInboundToken = components["schemas"]["CheckInboundTokenDto"];
export type CheckInboundTokenRotateResult = components["schemas"]["CheckInboundTokenRotateResultDto"];

export const checkTypesApi = {
  list: () => api.get<CheckTypeMeta[]>(ENDPOINTS.CHECK_TYPES).then((r) => r.data),
};

export const checksApi = {
  listAll: () => api.get<CheckSummary[]>(ENDPOINTS.CHECKS).then((r) => r.data),

  listForService: (serviceSlug: string) =>
    api.get<Check[]>(ENDPOINTS.SERVICE_CHECKS(serviceSlug)).then((r) => r.data),

  get: (serviceSlug: string, checkSlug: string) =>
    api.get<Check>(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug)).then((r) => r.data),

  create: (serviceSlug: string, data: CreateCheckRequest) =>
    api.post<Check>(ENDPOINTS.SERVICE_CHECKS(serviceSlug), data).then((r) => r.data),

  update: (serviceSlug: string, checkSlug: string, data: Partial<UpdateCheckRequest>) =>
    api.put<Check>(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug), data).then((r) => r.data),

  delete: (serviceSlug: string, checkSlug: string) =>
    api.delete(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug)),

  run: (serviceSlug: string, checkSlug: string) =>
    api.post(ENDPOINTS.SERVICE_CHECK_RUN(serviceSlug, checkSlug)),

  // Dry-run a testable (Script) check against the live target: runs in debug mode, captures console.log,
  // and returns the raw verdict without persisting or alerting. `typeDataJson` tests unsaved edits.
  test: (serviceSlug: string, checkSlug: string, typeDataJson?: string) =>
    api
      .post<ScriptTestResult>(ENDPOINTS.SERVICE_CHECK_TEST(serviceSlug, checkSlug), { typeDataJson })
      .then((r) => r.data),

  // Inbound-token checks (push-based, e.g. Heartbeat): read the token info, or rotate it (raw token once).
  inboundToken: (serviceSlug: string, checkSlug: string) =>
    api
      .get<CheckInboundToken>(ENDPOINTS.SERVICE_CHECK_INBOUND_TOKEN(serviceSlug, checkSlug))
      .then((r) => r.data),

  rotateInboundToken: (serviceSlug: string, checkSlug: string) =>
    api
      .post<CheckInboundTokenRotateResult>(ENDPOINTS.SERVICE_CHECK_INBOUND_TOKEN_ROTATE(serviceSlug, checkSlug))
      .then((r) => r.data),

  logs: (
    serviceSlug: string,
    checkSlug: string,
    params?: { limit?: number; region?: string; from?: string; to?: string }
  ) =>
    api
      .get<CheckDataPoint[]>(ENDPOINTS.SERVICE_CHECK_LOGS(serviceSlug, checkSlug), { params })
      .then((r) => r.data),

  history: (serviceSlug: string, checkSlug: string, days = 14) =>
    api
      .get<CheckDailyStats[]>(ENDPOINTS.SERVICE_CHECK_HISTORY(serviceSlug, checkSlug), { params: { days } })
      .then((r) => r.data),
};
