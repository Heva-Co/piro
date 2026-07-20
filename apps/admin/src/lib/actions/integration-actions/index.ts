import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type IntegrationActionDescriptor = components["schemas"]["IntegrationActionDescriptorDto"];
export type ExternalReference = components["schemas"]["ExternalReferenceDto"];
export type ActionContext = components["schemas"]["ActionContext"];
export type IntegrationActionResult = components["schemas"]["IntegrationActionResultDto"];

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

  /** Pre-fill an action's dialog for a target (RFC 0012 §4.6). Shaped like the action's input. */
  draft: (integrationId: string, actionId: string, context: ActionContext, targetId: number) =>
    api
      .get<Record<string, unknown>>(
        ENDPOINTS.INTEGRATION_ACTION_DRAFT(integrationId, actionId, context, targetId),
      )
      .then((r) => r.data),

  /** Resolve runtime options for a [DynamicOptions] field (RFC 0012), e.g. Jira projects/issue types. */
  options: (integrationId: string, sourceKey: string, dependsOn?: string) =>
    api
      .get<{ value: string; label: string }[]>(
        ENDPOINTS.INTEGRATION_ACTION_OPTIONS(integrationId, sourceKey, dependsOn),
      )
      .then((r) => r.data),

  /** Execute an action and get back the external reference it created (RFC 0012 §4.4). */
  execute: (
    integrationId: string,
    actionId: string,
    body: { context: ActionContext; targetId: number; input: Record<string, unknown> },
  ) =>
    api
      .post<IntegrationActionResult>(
        ENDPOINTS.INTEGRATION_ACTION_EXECUTE(integrationId, actionId),
        body,
      )
      .then((r) => r.data),
};
