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
  comments: IncidentComment[];
  createdAt: string;
  updatedAt: string;
  acknowledgedAt?: number;
  acknowledgedBy?: string;
  currentImpact: string;
  impactChanges: { timestamp: number; impact: string }[];
}

export interface IncidentComment {
  id: number;
  comment: string;
  commentedAt: number;
  status: string;
  visibility: IncidentVisibilityKey;
  createdAt: string;
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

  comments: (id: number | string) =>
    api.get<IncidentComment[]>(ENDPOINTS.INCIDENT_COMMENTS(id)).then((r) => r.data),

  addComment: (id: number | string, comment: string, status: string, visibility: IncidentVisibilityKey = "Private") =>
    api
      .post<IncidentComment>(ENDPOINTS.INCIDENT_COMMENTS(id), { comment, status, visibility })
      .then((r) => r.data),

  updateComment: (id: number | string, commentId: number | string, comment: string, status: string, visibility: IncidentVisibilityKey) =>
    api
      .put<IncidentComment>(ENDPOINTS.INCIDENT_COMMENT(id, commentId), { comment, status, visibility })
      .then((r) => r.data),

  deleteComment: (id: number | string, commentId: number | string) =>
    api.delete(ENDPOINTS.INCIDENT_COMMENT(id, commentId)),

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
