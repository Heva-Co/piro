import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type EscalationPolicy = components["schemas"]["EscalationPolicyDto"];
export type EscalationStep = components["schemas"]["EscalationStepDto"];
export type EscalationPolicyPage = components["schemas"]["EscalationPolicyPageDto"];
export type UpsertEscalationPolicyRequest = components["schemas"]["UpsertEscalationPolicyRequest"];
export type UpsertEscalationStepRequest = components["schemas"]["UpsertEscalationStepRequest"];

export const escalationApi = {
  list: (params?: { page?: number; pageSize?: number }) =>
    api.get<EscalationPolicyPage>(ENDPOINTS.ESCALATION_POLICIES, { params }).then((r) => r.data),
  get: (id: number | string) =>
    api.get<EscalationPolicy>(ENDPOINTS.ESCALATION_POLICY(id)).then((r) => r.data),
  create: (data: UpsertEscalationPolicyRequest) =>
    api.post<EscalationPolicy>(ENDPOINTS.ESCALATION_POLICIES, data).then((r) => r.data),
  update: (id: number | string, data: UpsertEscalationPolicyRequest) =>
    api.put<EscalationPolicy>(ENDPOINTS.ESCALATION_POLICY(id), data).then((r) => r.data),
  delete: (id: number | string) => api.delete(ENDPOINTS.ESCALATION_POLICY(id)),
};
