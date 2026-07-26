import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type Saml2ProviderConfig = components["schemas"]["Saml2ProviderConfigDto"];
export type UpsertSaml2Provider = components["schemas"]["UpsertSaml2ProviderRequest"];
export type Saml2ProviderInfo = components["schemas"]["Saml2ProviderInfo"];
export type Saml2MetadataResult = components["schemas"]["Saml2MetadataResult"];

export const samlApi = {
  list: () => api.get<Saml2ProviderConfig[]>(ENDPOINTS.SAML_CONFIG).then((r) => r.data),

  upsert: (data: UpsertSaml2Provider) => api.put(ENDPOINTS.SAML_CONFIG, data),

  delete: (id: string) => api.delete(ENDPOINTS.SAML_CONFIG_DETAIL(id)),

  /** Validates a saved provider's configuration (parseable cert, present endpoints). */
  test: (providerId: string) =>
    api
      .post<{ success: boolean; message: string }>(ENDPOINTS.SAML_CONFIG_TEST, { providerId })
      .then((r) => r.data),

  /** Parses an uploaded IdP metadata XML document into entity ID, SSO URL, and signing certificate. */
  parseMetadata: (metadataXml: string) =>
    api
      .post<Saml2MetadataResult>(ENDPOINTS.SAML_CONFIG_PARSE_METADATA, { metadataXml })
      .then((r) => r.data),
};
