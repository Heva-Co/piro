import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type NotificationSubscription = components["schemas"]["NotificationSubscriptionDto"];
export type NotificationSubscriptionPage = components["schemas"]["NotificationSubscriptionPageDto"];
export type UpsertNotificationSubscriptionRequest = components["schemas"]["UpsertNotificationSubscriptionRequest"];
export type NotificationEventCatalog = components["schemas"]["NotificationEventCatalogDto"];
export type NotificationTargetKind = components["schemas"]["NotificationTargetKind"];

export const notificationSubscriptionsApi = {
  list: (params?: { page?: number; pageSize?: number }) =>
    api.get<NotificationSubscriptionPage>(ENDPOINTS.NOTIFICATION_SUBSCRIPTIONS, { params }).then((r) => r.data),
  get: (id: string) =>
    api.get<NotificationSubscription>(ENDPOINTS.NOTIFICATION_SUBSCRIPTION(id)).then((r) => r.data),
  create: (data: UpsertNotificationSubscriptionRequest) =>
    api.post<NotificationSubscription>(ENDPOINTS.NOTIFICATION_SUBSCRIPTIONS, data).then((r) => r.data),
  update: (id: string, data: UpsertNotificationSubscriptionRequest) =>
    api.put<NotificationSubscription>(ENDPOINTS.NOTIFICATION_SUBSCRIPTION(id), data).then((r) => r.data),
  delete: (id: string) => api.delete(ENDPOINTS.NOTIFICATION_SUBSCRIPTION(id)),
  eventCatalog: () =>
    api.get<NotificationEventCatalog[]>(ENDPOINTS.NOTIFICATION_EVENT_CATALOG).then((r) => r.data),
};
