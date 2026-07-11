import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { IncidentVisibilityKey } from "@/constants/incidents";

export interface IncidentService {
  serviceSlug: string;
  impact: string;
  triggeringCheckSlug?: string | null;
}

export interface Incident {
  id: number;
  title: string;
  status: string;
  isResolved: boolean;
  startDateTime: number;
  endDateTime?: number | null;
  isGlobal: boolean;
  source?: string | null;
  visibility: IncidentVisibilityKey;
  mergedIntoIncidentId?: number | null;
  services: IncidentService[];
  timeline: IncidentTimelineEvent[];
  createdAt: string;
  updatedAt: string;
  acknowledgedAt?: number;
  acknowledgedBy?: string;
  currentImpact: string;
  impactChanges: { timestamp: number; impact: string }[];
}

export interface IncidentTimelineEvent {
  id: number;
  type: string;
  occurredAt: string;
  actorName?: string | null;
  comment?: string | null;
  oldStatus?: string | null;
  newStatus?: string | null;
  visibility: IncidentVisibilityKey;
  relatedIncidentId?: number | null;
}

export const incidentsApi = {
  list: (filter = "active") =>
    api.get<Incident[]>(`${ENDPOINTS.INCIDENTS}?filter=${filter}`).then((r) => r.data),

  get: (id: number | string) =>
    api.get<Incident>(ENDPOINTS.INCIDENT(id)).then((r) => r.data),

  create: (data: { title: string; startDateTime: number; status: string; isGlobal: boolean }) =>
    api.post<Incident>(ENDPOINTS.INCIDENTS, data).then((r) => r.data),

  update: (id: number | string, data: Partial<Omit<Incident, "id">>) =>
    api.put<Incident>(ENDPOINTS.INCIDENT(id), data).then((r) => r.data),

  delete: (id: number | string) => api.delete(ENDPOINTS.INCIDENT(id)),

  addTimelineComment: (id: number | string, comment: string, status: string | null, visibility: IncidentVisibilityKey = "Private") =>
    api
      .post<IncidentTimelineEvent>(ENDPOINTS.INCIDENT_UPDATES(id), { comment, status, visibility })
      .then((r) => r.data),

  updateTimelineComment: (id: number | string, eventId: number | string, comment: string, visibility: IncidentVisibilityKey) =>
    api
      .put<IncidentTimelineEvent>(ENDPOINTS.INCIDENT_UPDATE(id, eventId), { comment, visibility })
      .then((r) => r.data),

  deleteTimelineComment: (id: number | string, eventId: number | string) =>
    api.delete(ENDPOINTS.INCIDENT_UPDATE(id, eventId)),

  addService: (id: number | string, slug: string, impact: string) =>
    api.post(ENDPOINTS.INCIDENT_SERVICES(id), { serviceSlug: slug, impact }),

  setServices: (id: number | string, services: { serviceSlug: string; impact: string }[]) =>
    api.put<Incident>(ENDPOINTS.INCIDENT_SERVICES(id), { services }).then((r) => r.data),

  acknowledge: (id: number | string) =>
    api.post<Incident>(ENDPOINTS.INCIDENT_ACKNOWLEDGE(id)).then((r) => r.data),

  removeService: (id: number | string, slug: string) =>
    api.delete(ENDPOINTS.INCIDENT_SERVICE(id, slug)),

  publish: (id: number | string) =>
    api.post(`/api/v1/incidents/${id}/publish`),

  unpublish: (id: number | string) =>
    api.post(`/api/v1/incidents/${id}/unpublish`),
};
