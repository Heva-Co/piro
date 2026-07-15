import { get } from "@/src/lib/http";
import type { components } from "@/src/lib/api-types";
import type { ServiceStatus } from "@/src/lib/actions/services";

export type IncidentStatus = components["schemas"]["IncidentStatus"];

/** Not a generated enum — the backend's IncidentImpactChangeDto.impact is a plain string. */
export type IncidentImpactChange = Omit<components["schemas"]["IncidentImpactChangeDto"], "impact"> & {
  impact: ServiceStatus;
};

/** Not a generated enum — the backend's IncidentTimelineEventDto.type is a plain string. Kept as a manual union so comparisons here stay typo-checked. */
export type TimelineEventType =
  | "Created"
  | "StatusChanged"
  | "CommentPosted"
  | "Acknowledged"
  | "ServiceAdded"
  | "ServiceRemoved"
  | "MergedTo"
  | "MergedFrom"
  | "Published"
  | "Unpublished"
  | "AlertFired";

/** A single Public timeline event — only CommentPosted carries user-facing text/status. */
export type IncidentTimelineEvent = Omit<components["schemas"]["IncidentTimelineEventDto"], "type"> & {
  type: TimelineEventType;
};

export type IncidentTimelinePage = Omit<components["schemas"]["IncidentTimelinePageDto"], "items"> & {
  items: IncidentTimelineEvent[];
};

/** Service affected by an incident — no impact level or triggering check exposed publicly. */
export type IncidentService = components["schemas"]["PublicIncidentServiceDto"];

/** Public-facing incident — internal fields (source, acknowledgedBy, escalation state) are never sent by the API. */
export type Incident = Omit<components["schemas"]["PublicIncidentDto"], "impactChanges"> & {
  impactChanges: IncidentImpactChange[];
};

export const incidentsApi = {
  list: (includeResolved = false) =>
    get<Incident[]>(`/public/incidents?includeResolved=${includeResolved}`),

  get: (id: number | string) => get<Incident>(`/public/incidents/${id}`, 0),

  timeline: (id: number | string, page = 1, pageSize = 20) =>
    get<IncidentTimelinePage>(`/public/incidents/${id}/timeline?page=${page}&pageSize=${pageSize}`, 0),
};
