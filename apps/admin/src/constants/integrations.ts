/** Sentinel the API sends in place of a real secret value. Submitting it back unchanged means "keep the existing secret". */
export const MASKED_SECRET_VALUE = "__MASKED__";

export type IntegrationCategory = "thirdparty" | "notification";

export interface IntegrationTypeMeta {
  label: string;
  icon: string;
  iconClass?: string;
  upcoming?: boolean;
  alpha?: boolean;
  category: IntegrationCategory;
}

export const INTEGRATION_TYPE_MAP = {
  // Third-party
  GoogleCloud: { label: "Google Cloud",    icon: "logos:google-cloud",        category: "thirdparty" },
  Jira:        { label: "Jira",            icon: "logos:jira",                category: "thirdparty" },
  AWS:         { label: "Amazon AWS",      icon: "logos:aws",                 category: "thirdparty", upcoming: true },
  Azure:       { label: "Azure",           icon: "logos:microsoft-azure",     category: "thirdparty", upcoming: true },
  GitHub:      { label: "GitHub",          icon: "simple-icons:github",       category: "thirdparty", iconClass: "dark:invert", upcoming: true },
  // Notification — Email is deliberately absent: it uses the platform SMTP/Resend config
  // (Configuration > Email), never a per-integration credential, so it's not creatable here.
  PagerDuty:   { label: "PagerDuty",       icon: "logos:pagerduty",           category: "notification", alpha: true },
  MSTeams:     { label: "Microsoft Teams", icon: "logos:microsoft-teams",     category: "notification", alpha: true },
  Telegram:    { label: "Telegram",        icon: "logos:telegram",            category: "notification" },
  Twilio:      { label: "Twilio",          icon: "logos:twilio-icon",         category: "notification", alpha: true },
  Opsgenie:    { label: "Opsgenie",        icon: "simple-icons:opsgenie",     category: "notification", iconClass: "dark:invert", alpha: true },
  Pushover:    { label: "Pushover",        icon: "tabler:brand-pushover",            category: "notification", alpha: true },
  Ntfy:        { label: "Ntfy",            icon: "simple-icons:ntfy",         category: "notification", iconClass: "dark:invert" },
} as const satisfies Record<string, IntegrationTypeMeta>;

export type IntegrationTypeKey = keyof typeof INTEGRATION_TYPE_MAP;

export const INTEGRATION_TYPES: (IntegrationTypeMeta & { value: IntegrationTypeKey })[] = Object.entries(INTEGRATION_TYPE_MAP).map(
  ([value, meta]) => ({ value: value as IntegrationTypeKey, ...(meta as IntegrationTypeMeta) })
);

export const INTEGRATION_TYPES_THIRDPARTY = INTEGRATION_TYPES.filter((t) => t.category === "thirdparty");
export const INTEGRATION_TYPES_NOTIFICATION = INTEGRATION_TYPES.filter((t) => t.category === "notification");
