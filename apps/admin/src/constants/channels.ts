export type ChannelTypeKey =
  | "Webhook" | "Email" | "Slack" | "PagerDuty" | "MSTeams"
  | "Telegram" | "TwilioSms" | "GoogleChat" | "Discord"
  | "Opsgenie" | "Pushover" | "Ntfy";

export interface ChannelTypeMeta {
  label: string;
  icon: string;
  iconClass?: string;
}

export const CHANNEL_TYPE_MAP: Record<ChannelTypeKey, ChannelTypeMeta> = {
  Webhook:    { label: "Webhook",         icon: "lucide:webhook" },
  Email:      { label: "Email",           icon: "lucide:mail" },
  Slack:      { label: "Slack",           icon: "logos:slack-icon" },
  PagerDuty:  { label: "PagerDuty",       icon: "logos:pagerduty" },
  MSTeams:    { label: "Microsoft Teams", icon: "logos:microsoft-teams" },
  Telegram:   { label: "Telegram",        icon: "logos:telegram" },
  TwilioSms:  { label: "Twilio SMS",      icon: "logos:twilio-icon" },
  GoogleChat: { label: "Google Chat",     icon: "selfh:google-chat" },
  Discord:    { label: "Discord",         icon: "logos:discord-icon" },
  Opsgenie:   { label: "Opsgenie",        icon: "simple-icons:opsgenie", iconClass: "dark:invert" },
  Pushover:   { label: "Pushover",        icon: "selfh:pushover" },
  Ntfy:       { label: "Ntfy",            icon: "simple-icons:ntfy",     iconClass: "dark:invert" },
};

export const CHANNEL_TYPES = Object.entries(CHANNEL_TYPE_MAP).map(
  ([value, meta]) => ({ value: value as ChannelTypeKey, ...meta })
);
