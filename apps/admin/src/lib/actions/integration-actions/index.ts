import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type IntegrationActionDescriptor = components["schemas"]["IntegrationActionDescriptorDto"];
export type ExternalReference = components["schemas"]["ExternalReferenceDto"];
export type ActionContext = components["schemas"]["ActionContext"];

export const integrationActionsApi = {
  /** Discovery: which action buttons to render for an object of this context (RFC 0012 §4.4). */
  list: (context: ActionContext) =>
    api
      .get<IntegrationActionDescriptor[]>(ENDPOINTS.INTEGRATION_ACTIONS(context))
      .then((r) => r.data),

  /** Existing outbound references (e.g. a linked Jira ticket) for this object (RFC 0012 §4.5). */
  references: (context: ActionContext, targetId: number) =>
    api
      .get<ExternalReference[]>(ENDPOINTS.INTEGRATION_REFERENCES(context, targetId))
      .then((r) => r.data),
};
