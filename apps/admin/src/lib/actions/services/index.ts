import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type Service = components["schemas"]["ServiceDto"];
export type PaginatedServices = components["schemas"]["PaginatedResponseOfServiceDto"];
export type UpdateServiceRequest = components["schemas"]["UpdateServiceRequest"];

export const servicesApi = {
  list: (params?: { page?: number; pageSize?: number; search?: string }) =>
    api.get<PaginatedServices>(ENDPOINTS.SERVICES, { params }).then((r) => r.data),

  get: (slug: string) => api.get<Service>(ENDPOINTS.SERVICE(slug)).then((r) => r.data),

  create: (data: Omit<Service, "id" | "currentStatus" | "escalationPolicyName">) =>
    api.post<Service>(ENDPOINTS.SERVICES, data).then((r) => r.data),

  update: (slug: string, data: Partial<UpdateServiceRequest>) =>
    api.put<Service>(ENDPOINTS.SERVICE(slug), data).then((r) => r.data),

  delete: (slug: string) => api.delete(ENDPOINTS.SERVICE(slug)),
};
