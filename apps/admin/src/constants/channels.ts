export type ChannelTypeKey =
  | "Webhook" | "Email" | "Slack" | "PagerDuty" | "MSTeams"
  | "Telegram" | "TwilioSms" | "GoogleChat" | "Discord"
  | "Opsgenie" | "Pushover" | "Ntfy";

export interface ChannelTypeMeta {
  label: string;
  icon: string;
  iconClass?: string;
  alpha?: boolean;
}

// Stable: Email (system), Telegram, GoogleChat. All others are alpha.
export const CHANNEL_TYPE_MAP: Record<ChannelTypeKey, ChannelTypeMeta> = {
  Webhook:    { label: "Webhook",         icon: "lucide:webhook",            alpha: true },
  Email:      { label: "Email",           icon: "lucide:mail" },
  Slack:      { label: "Slack",           icon: "logos:slack-icon",          alpha: true },
  PagerDuty:  { label: "PagerDuty",       icon: "logos:pagerduty",           alpha: true },
  MSTeams:    { label: "Microsoft Teams", icon: "logos:microsoft-teams",     alpha: true },
  Telegram:   { label: "Telegram",        icon: "logos:telegram" },
  TwilioSms:  { label: "Twilio SMS",      icon: "logos:twilio-icon",         alpha: true },
  GoogleChat: { label: "Google Chat",     icon: "selfh:google-chat" },
  Discord:    { label: "Discord",         icon: "logos:discord-icon",        alpha: true },
  Opsgenie:   { label: "Opsgenie",        icon: "simple-icons:opsgenie",     iconClass: "dark:invert", alpha: true },
  Pushover:   { label: "Pushover",        icon: "selfh:pushover",            alpha: true },
  Ntfy:       { label: "Ntfy",            icon: "simple-icons:ntfy",         iconClass: "dark:invert", alpha: true },
};

export const CHANNEL_TYPES = Object.entries(CHANNEL_TYPE_MAP).map(
  ([value, meta]) => ({ value: value as ChannelTypeKey, ...meta })
);
