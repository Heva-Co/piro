import { z } from "zod";

const baseIntegrationSchema = z.object({
  name: z.string().min(1, "Name is required"),
  description: z.string().optional().default(""),
  type: z.string(),
  // GoogleCloud
  serviceAccountJson: z.string().optional().default(""),
  // Jira
  jiraBaseUrl: z.string().optional().default(""),
  jiraEmail: z.string().optional().default(""),
  jiraApiToken: z.string().optional().default(""),
  jiraProjectKey: z.string().optional().default(""),
  jiraIssueType: z.string().optional().default(""),
  // Slack
  slackBotToken: z.string().optional().default(""),
  // PagerDuty
  pdRoutingKey: z.string().optional().default(""),
  // MSTeams
  teamsWebhookUrl: z.string().optional().default(""),
  // Telegram
  tgBotToken: z.string().optional().default(""),
  // Twilio
  twAccountSid: z.string().optional().default(""),
  twAuthToken: z.string().optional().default(""),
  twFromNumber: z.string().optional().default(""),
  // Opsgenie
  ogApiKey: z.string().optional().default(""),
  ogRegion: z.string().optional().default(""),
  // Pushover
  poAppToken: z.string().optional().default(""),
  // Ntfy
  ntfyServerUrl: z.string().optional().default(""),
  ntfyToken: z.string().optional().default(""),
});

/**
 * Per-provider required fields, checked centrally instead of scattered `register(field, { required })`
 * calls in each *Config.tsx component — a single source of truth for "which fields matter for this type".
 * Keyed by `type` (matches IntegrationTypeKey); providers not listed (Email, Slack) have no required fields.
 */
const REQUIRED_FIELDS_BY_TYPE: Partial<Record<string, { field: keyof IntegrationFormValues; message: string }[]>> = {
  GoogleCloud: [{ field: "serviceAccountJson", message: "Service Account JSON is required" }],
  Jira: [
    { field: "jiraBaseUrl", message: "Base URL is required" },
    { field: "jiraEmail", message: "Email is required" },
    { field: "jiraApiToken", message: "API Token is required" },
  ],
  PagerDuty: [{ field: "pdRoutingKey", message: "Routing Key is required" }],
  MSTeams: [{ field: "teamsWebhookUrl", message: "Incoming Webhook URL is required" }],
  Telegram: [{ field: "tgBotToken", message: "Bot Token is required" }],
  Twilio: [
    { field: "twAccountSid", message: "Account SID is required" },
    { field: "twAuthToken", message: "Auth Token is required" },
    { field: "twFromNumber", message: "From number is required" },
  ],
  Opsgenie: [{ field: "ogApiKey", message: "API Key is required" }],
  Pushover: [{ field: "poAppToken", message: "App Token is required" }],
  Ntfy: [{ field: "ntfyServerUrl", message: "Server URL is required" }],
};

const JIRA_URL_PATTERN = /^https:\/\/[a-zA-Z0-9-]+\.atlassian\.net\/?$/;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export const integrationFormSchema = baseIntegrationSchema.superRefine((values, ctx) => {
  const required = REQUIRED_FIELDS_BY_TYPE[values.type];
  if (required) {
    for (const { field, message } of required) {
      if (!values[field] || String(values[field]).trim() === "") {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message, path: [field] });
      }
    }
  }

  if (values.type === "Jira") {
    if (values.jiraBaseUrl && !JIRA_URL_PATTERN.test(values.jiraBaseUrl)) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be a valid Atlassian URL (https://your-org.atlassian.net)", path: ["jiraBaseUrl"] });
    }
    if (values.jiraEmail && !EMAIL_PATTERN.test(values.jiraEmail)) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Enter a valid email", path: ["jiraEmail"] });
    }
  }
});

export type IntegrationFormValues = z.infer<typeof baseIntegrationSchema>;

export const INTEGRATION_FORM_DEFAULTS: IntegrationFormValues = {
  name: "", description: "", type: "Slack",
  serviceAccountJson: "",
  jiraBaseUrl: "", jiraEmail: "", jiraApiToken: "", jiraProjectKey: "", jiraIssueType: "",
  slackBotToken: "",
  pdRoutingKey: "",
  teamsWebhookUrl: "",
  tgBotToken: "",
  twAccountSid: "", twAuthToken: "", twFromNumber: "",
  ogApiKey: "", ogRegion: "US",
  poAppToken: "",
  ntfyServerUrl: "https://ntfy.sh", ntfyToken: "",
};
