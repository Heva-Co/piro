import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type NotificationDeliveryLog = components["schemas"]["NotificationDeliveryLogDto"];
export type NotificationDeliveryLogPage = components["schemas"]["NotificationDeliveryLogPageDto"];
export type DeliveryStatus = components["schemas"]["DeliveryStatus"];

export const deliveryLogsApi = {
  list: (params?: { page?: number; pageSize?: number; status?: DeliveryStatus }) =>
    api.get<NotificationDeliveryLogPage>(ENDPOINTS.NOTIFICATION_DELIVERY_LOGS, { params }).then((r) => r.data),
};
