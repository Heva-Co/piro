import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type Saml2ProviderConfig = components["schemas"]["Saml2ProviderConfigDto"];
export type UpsertSaml2Provider = components["schemas"]["UpsertSaml2ProviderRequest"];
export type Saml2ProviderInfo = components["schemas"]["Saml2ProviderInfo"];

export const samlApi = {
  list: () => api.get<Saml2ProviderConfig[]>(ENDPOINTS.SAML_CONFIG).then((r) => r.data),

  upsert: (data: UpsertSaml2Provider) => api.put(ENDPOINTS.SAML_CONFIG, data),

  /** Validates a saved provider's configuration (parseable cert, present endpoints). */
  test: (providerId: string) =>
    api
      .post<{ success: boolean; message: string }>(ENDPOINTS.SAML_CONFIG_TEST, { providerId })
      .then((r) => r.data),
};
