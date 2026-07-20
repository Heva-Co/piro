import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type Postmortem = components["schemas"]["PostmortemDto"];
export type PostmortemListItem = components["schemas"]["PostmortemListItemDto"];
export type PostmortemFieldValue = components["schemas"]["PostmortemFieldValueDto"];
export type PostmortemFieldDefinition = components["schemas"]["PostmortemFieldDefinitionDto"];
export type PostmortemIncidentRef = components["schemas"]["PostmortemIncidentRefDto"];
export type PostmortemTimelineItem = components["schemas"]["PostmortemTimelineItemDto"];
export type PostmortemStatus = components["schemas"]["PostmortemStatus"];
export type PostmortemFieldType = components["schemas"]["PostmortemFieldType"];
export type CreatePostmortemRequest = components["schemas"]["CreatePostmortemRequest"];
export type UpdatePostmortemRequest = components["schemas"]["UpdatePostmortemRequest"];
export type PostmortemFieldValueUpdate = components["schemas"]["PostmortemFieldValueUpdate"];
export type CreateTimelineEntryRequest = components["schemas"]["CreateTimelineEntryRequest"];
export type UpdateTimelineEntryRequest = components["schemas"]["UpdateTimelineEntryRequest"];
export type PostmortemIncidentSuggestion = components["schemas"]["PostmortemIncidentSuggestionDto"];

export const postmortemsApi = {
  list: () => api.get<PostmortemListItem[]>(ENDPOINTS.POSTMORTEMS).then((r) => r.data),

  get: (id: number | string) => api.get<Postmortem>(ENDPOINTS.POSTMORTEM(id)).then((r) => r.data),

  fieldDefinitions: () =>
    api.get<PostmortemFieldDefinition[]>(ENDPOINTS.POSTMORTEM_FIELD_DEFINITIONS).then((r) => r.data),

  create: (data: CreatePostmortemRequest) =>
    api.post<Postmortem>(ENDPOINTS.POSTMORTEMS, data).then((r) => r.data),

  update: (id: number | string, data: UpdatePostmortemRequest) =>
    api.put<Postmortem>(ENDPOINTS.POSTMORTEM(id), data).then((r) => r.data),

  publish: (id: number | string) => api.post(ENDPOINTS.POSTMORTEM_PUBLISH(id)),

  unpublish: (id: number | string) => api.post(ENDPOINTS.POSTMORTEM_UNPUBLISH(id)),

  delete: (id: number | string) => api.delete(ENDPOINTS.POSTMORTEM(id)),

  linkIncident: (id: number | string, incidentId: number) =>
    api.post<Postmortem>(ENDPOINTS.POSTMORTEM_INCIDENTS(id), { incidentId }).then((r) => r.data),

  unlinkIncident: (id: number | string, incidentId: number) =>
    api.delete<Postmortem>(ENDPOINTS.POSTMORTEM_INCIDENT(id, incidentId)).then((r) => r.data),

  incidentSuggestions: (id: number | string) =>
    api
      .get<PostmortemIncidentSuggestion[]>(ENDPOINTS.POSTMORTEM_INCIDENT_SUGGESTIONS(id))
      .then((r) => r.data),

  addTimelineEntry: (id: number | string, data: CreateTimelineEntryRequest) =>
    api.post<Postmortem>(ENDPOINTS.POSTMORTEM_TIMELINE(id), data).then((r) => r.data),

  updateTimelineEntry: (id: number | string, entryId: number, data: UpdateTimelineEntryRequest) =>
    api.put<Postmortem>(ENDPOINTS.POSTMORTEM_TIMELINE_ENTRY(id, entryId), data).then((r) => r.data),

  deleteTimelineEntry: (id: number | string, entryId: number) =>
    api.delete<Postmortem>(ENDPOINTS.POSTMORTEM_TIMELINE_ENTRY(id, entryId)).then((r) => r.data),
};
