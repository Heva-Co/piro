import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type IntegrationTypeMeta = components["schemas"]["IntegrationTypeMetaDto"];
export type ConfigFieldSchema = components["schemas"]["ConfigFieldSchemaDto"];

export const integrationTypesApi = {
  list: () => api.get<IntegrationTypeMeta[]>(ENDPOINTS.INTEGRATION_TYPES).then((r) => r.data),
};

export type Integration = components["schemas"]["IntegrationDto"];
export type CreateIntegrationRequest = components["schemas"]["CreateIntegrationRequest"];
export type UpdateIntegrationRequest = components["schemas"]["UpdateIntegrationRequest"];
export type WebhookRequestLog = components["schemas"]["WebhookRequestLogDto"];

export const integrationsApi = {
  list: () => api.get<Integration[]>(ENDPOINTS.INTEGRATIONS).then((r) => r.data),
  get: (id: string) => api.get<Integration>(ENDPOINTS.INTEGRATION(id)).then((r) => r.data),
  create: (data: CreateIntegrationRequest) =>
    api.post<Integration>(ENDPOINTS.INTEGRATIONS, data).then((r) => r.data),
  update: (id: string, data: UpdateIntegrationRequest) =>
    api.put<Integration>(ENDPOINTS.INTEGRATION(id), data).then((r) => r.data),
  delete: (id: string) => api.delete(ENDPOINTS.INTEGRATION(id)),
  getWebhookLogs: (id: string) =>
    api.get<WebhookRequestLog[]>(ENDPOINTS.INTEGRATION_WEBHOOK_LOGS(id)).then((r) => r.data),
  regenerateGeneratedFields: (id: string) =>
    api.post<Integration>(ENDPOINTS.INTEGRATION_REGENERATE_GENERATED_FIELDS(id)).then((r) => r.data),
};
