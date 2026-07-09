import { INTEGRATION_TYPE_MAP, type IntegrationTypeMeta, type IntegrationTypeKey } from "./integrations";

// Channel types are the notification subset of integrations
export type ChannelTypeKey = IntegrationTypeKey;

export const CHANNEL_TYPE_MAP = Object.fromEntries(
  (Object.entries(INTEGRATION_TYPE_MAP) as [string, IntegrationTypeMeta][])
    .filter(([, meta]) => meta.category === "notification")
    .map(([key, meta]) => [key, { label: meta.label, icon: meta.icon, iconClass: meta.iconClass, alpha: meta.alpha }])
) as Record<ChannelTypeKey, { label: string; icon: string; iconClass?: string; alpha?: boolean }>;

export const CHANNEL_TYPES = Object.entries(CHANNEL_TYPE_MAP).map(
  ([value, meta]) => ({ value: value as ChannelTypeKey, ...meta })
);

/** Types that carry credentials in the channel itself — no global Integration needed. */
export const CHANNEL_ONLY_TYPES = new Set<ChannelTypeKey>([
  "Discord", "Email", "Webhook", "GoogleChat", "Slack",
]);
