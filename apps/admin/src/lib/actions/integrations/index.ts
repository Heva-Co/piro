import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type IntegrationTypeMeta = components["schemas"]["IntegrationTypeMetaDto"];
export type ConfigFieldSchema = components["schemas"]["ConfigFieldSchemaDto"];

export const integrationTypesApi = {
  list: () => api.get<IntegrationTypeMeta[]>(ENDPOINTS.INTEGRATION_TYPES).then((r) => r.data),
};
