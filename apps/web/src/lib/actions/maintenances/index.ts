import { get } from "@/src/lib/http";
import type { components } from "@/src/lib/api-types";

export type MaintenanceStatus = components["schemas"]["MaintenanceStatus"];
export type MaintenanceEventStatus = components["schemas"]["MaintenanceEventStatus"];
export type MaintenanceDisplayStatus = components["schemas"]["MaintenanceDisplayStatus"];
export type MaintenanceEvent = components["schemas"]["MaintenanceEventDto"];
export type Maintenance = components["schemas"]["MaintenanceDto"];

export const maintenancesApi = {
  list: () => get<Maintenance[]>("/public/maintenances"),
};
