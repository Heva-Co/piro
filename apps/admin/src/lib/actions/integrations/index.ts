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

export type OAuthConnectionStatus = components["schemas"]["OAuthConnectionStatusDto"];

export type OAuthRedirectUri = components["schemas"]["OAuthRedirectUriDto"];

export const integrationOAuthApi = {
  redirectUri: () =>
    api.get<OAuthRedirectUri>(ENDPOINTS.INTEGRATION_OAUTH_REDIRECT_URI).then((r) => r.data),
  status: (id: string) =>
    api.get<OAuthConnectionStatus>(ENDPOINTS.INTEGRATION_OAUTH_STATUS(id)).then((r) => r.data),
  connect: (id: string) =>
    api
      .get<{ authorizationUrl: string }>(ENDPOINTS.INTEGRATION_OAUTH_CONNECT(id))
      .then((r) => r.data),
  disconnect: (id: string) => api.post(ENDPOINTS.INTEGRATION_OAUTH_DISCONNECT(id)),
  callback: (code: string, state: string) =>
    api
      .post<{ integrationId: string; provider: string; connected: boolean }>(
        ENDPOINTS.INTEGRATION_OAUTH_CALLBACK,
        { code, state },
      )
      .then((r) => r.data),
  discover: (id: string) =>
    api.get<DiscoveredResource[]>(ENDPOINTS.INTEGRATION_OAUTH_DISCOVER(id)).then((r) => r.data),
};

export type DiscoveredResource = components["schemas"]["DiscoveredResourceDto"];
export type ServiceIntegrationMapping = components["schemas"]["ServiceIntegrationMappingDto"];
export type UpsertServiceIntegrationMappingRequest =
  components["schemas"]["UpsertServiceIntegrationMappingRequest"];

export const serviceIntegrationMappingApi = {
  list: (serviceId: number) =>
    api
      .get<ServiceIntegrationMapping[]>(ENDPOINTS.SERVICE_INTEGRATION_MAPPINGS(serviceId))
      .then((r) => r.data),
  upsert: (serviceId: number, data: UpsertServiceIntegrationMappingRequest) =>
    api
      .put<ServiceIntegrationMapping>(ENDPOINTS.SERVICE_INTEGRATION_MAPPINGS(serviceId), data)
      .then((r) => r.data),
  remove: (serviceId: number, integrationId: string) =>
    api.delete(ENDPOINTS.SERVICE_INTEGRATION_MAPPING(serviceId, integrationId)),
};
