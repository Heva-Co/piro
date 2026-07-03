export type IntegrationTypeKey = "GoogleCloud" | "Jira" | "AWS" | "Azure" | "GitHub";

export interface IntegrationTypeMeta {
  label: string;
  icon: string;
  iconClass?: string;
  upcoming?: boolean;
}

export const INTEGRATION_TYPE_MAP: Record<IntegrationTypeKey, IntegrationTypeMeta> = {
  GoogleCloud: { label: "Google Cloud", icon: "logos:google-cloud" },
  Jira:        { label: "Jira",         icon: "logos:jira" },
  AWS:         { label: "Amazon AWS",   icon: "logos:aws",             upcoming: true },
  Azure:       { label: "Azure",        icon: "logos:microsoft-azure", upcoming: true },
  GitHub:      { label: "GitHub",       icon: "simple-icons:github",   iconClass: "dark:invert", upcoming: true },
};

export const INTEGRATION_TYPES = Object.entries(INTEGRATION_TYPE_MAP).map(
  ([value, meta]) => ({ value: value as IntegrationTypeKey, ...meta })
);
