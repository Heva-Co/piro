import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type Tag = components["schemas"]["TagDto"];
export type EntityTags = components["schemas"]["EntityTagsDto"];
export type CheckTags = components["schemas"]["CheckTagsDto"];
export type ReplaceTagsRequest = components["schemas"]["ReplaceTagsRequest"];

/// Tag read/write and autocomplete for services, checks, and workers (RFC 0008).
export const tagsApi = {
  getServiceTags: (id: number) =>
    api.get<EntityTags>(ENDPOINTS.SERVICE_TAGS(id)).then((r) => r.data),
  replaceServiceTags: (id: number, data: ReplaceTagsRequest) =>
    api.put<EntityTags>(ENDPOINTS.SERVICE_TAGS(id), data).then((r) => r.data),

  getCheckTags: (id: number) =>
    api.get<CheckTags>(ENDPOINTS.CHECK_TAGS(id)).then((r) => r.data),
  replaceCheckTags: (id: number, data: ReplaceTagsRequest) =>
    api.put<CheckTags>(ENDPOINTS.CHECK_TAGS(id), data).then((r) => r.data),

  getWorkerTags: (id: string) =>
    api.get<EntityTags>(ENDPOINTS.WORKER_TAGS(id)).then((r) => r.data),
  replaceWorkerTags: (id: string, data: ReplaceTagsRequest) =>
    api.put<EntityTags>(ENDPOINTS.WORKER_TAGS(id), data).then((r) => r.data),

  assignServiceSystemTag: (id: number, key: string, value?: string | null) =>
    api.put(ENDPOINTS.SERVICE_SYSTEM_TAG(id, key), { value: value ?? null }).then((r) => r.data),
  unassignServiceSystemTag: (id: number, key: string) =>
    api.delete(ENDPOINTS.SERVICE_SYSTEM_TAG(id, key)).then((r) => r.data),
  assignCheckSystemTag: (id: number, key: string, value?: string | null) =>
    api.put(ENDPOINTS.CHECK_SYSTEM_TAG(id, key), { value: value ?? null }).then((r) => r.data),
  unassignCheckSystemTag: (id: number, key: string) =>
    api.delete(ENDPOINTS.CHECK_SYSTEM_TAG(id, key)).then((r) => r.data),

  getRequiredWorkerTags: (id: number) =>
    api.get<EntityTags>(ENDPOINTS.CHECK_REQUIRED_WORKER_TAGS(id)).then((r) => r.data),
  replaceRequiredWorkerTags: (id: number, data: ReplaceTagsRequest) =>
    api.put<EntityTags>(ENDPOINTS.CHECK_REQUIRED_WORKER_TAGS(id), data).then((r) => r.data),

  keys: (prefix?: string, includeSystem?: boolean) =>
    api
      .get<string[]>(ENDPOINTS.TAG_KEYS, {
        params: { ...(prefix ? { prefix } : {}), ...(includeSystem ? { includeSystem: true } : {}) },
      })
      .then((r) => r.data),
  values: (key: string) =>
    api.get<string[]>(ENDPOINTS.TAG_VALUES, { params: { key } }).then((r) => r.data),
};
