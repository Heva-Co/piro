export type IntegrationTypeKey =
  // Third-party
  | "GoogleCloud" | "Jira" | "AWS" | "Azure" | "GitHub"
  // Notification (Email uses the system email service — no integration needed)
  | "Webhook" | "Slack" | "PagerDuty" | "MSTeams"
  | "Telegram" | "TwilioSms" | "GoogleChat" | "Discord"
  | "Opsgenie" | "Pushover" | "Ntfy";

export type IntegrationCategory = "thirdparty" | "notification";

export interface IntegrationTypeMeta {
  label: string;
  icon: string;
  iconClass?: string;
  upcoming?: boolean;
  alpha?: boolean;
  category: IntegrationCategory;
}

export const INTEGRATION_TYPE_MAP: Record<IntegrationTypeKey, IntegrationTypeMeta> = {
  // Third-party
  GoogleCloud: { label: "Google Cloud",    icon: "logos:google-cloud",        category: "thirdparty" },
  Jira:        { label: "Jira",            icon: "logos:jira",                category: "thirdparty" },
  AWS:         { label: "Amazon AWS",      icon: "logos:aws",                 category: "thirdparty", upcoming: true },
  Azure:       { label: "Azure",           icon: "logos:microsoft-azure",     category: "thirdparty", upcoming: true },
  GitHub:      { label: "GitHub",          icon: "simple-icons:github",       category: "thirdparty", iconClass: "dark:invert", upcoming: true },
  // Notification (Email omitted — uses system email service)
  // Stable: Telegram, GoogleChat. All others are alpha.
  Webhook:     { label: "Webhook",         icon: "lucide:webhook",            category: "notification", alpha: true },
  Slack:       { label: "Slack",           icon: "logos:slack-icon",          category: "notification", alpha: true },
  PagerDuty:   { label: "PagerDuty",       icon: "logos:pagerduty",           category: "notification", alpha: true },
  MSTeams:     { label: "Microsoft Teams", icon: "logos:microsoft-teams",     category: "notification", alpha: true },
  Telegram:    { label: "Telegram",        icon: "logos:telegram",            category: "notification" },
  TwilioSms:   { label: "Twilio SMS",      icon: "logos:twilio-icon",         category: "notification", alpha: true },
  GoogleChat:  { label: "Google Chat",     icon: "selfh:google-chat",         category: "notification" },
  Discord:     { label: "Discord",         icon: "logos:discord-icon",        category: "notification", alpha: true },
  Opsgenie:    { label: "Opsgenie",        icon: "simple-icons:opsgenie",     category: "notification", iconClass: "dark:invert", alpha: true },
  Pushover:    { label: "Pushover",        icon: "selfh:pushover",            category: "notification", alpha: true },
  Ntfy:        { label: "Ntfy",            icon: "simple-icons:ntfy",         category: "notification", iconClass: "dark:invert", alpha: true },
};

export const INTEGRATION_TYPES = Object.entries(INTEGRATION_TYPE_MAP).map(
  ([value, meta]) => ({ value: value as IntegrationTypeKey, ...meta })
);

export const INTEGRATION_TYPES_THIRDPARTY = INTEGRATION_TYPES.filter((t) => t.category === "thirdparty");
export const INTEGRATION_TYPES_NOTIFICATION = INTEGRATION_TYPES.filter((t) => t.category === "notification");
