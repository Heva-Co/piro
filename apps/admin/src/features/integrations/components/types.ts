import { z } from "zod";

export const integrationFormSchema = z.object({
  name: z.string().min(1, "Name is required"),
  description: z.string(),
  type: z.string(),
  // GoogleCloud
  serviceAccountJson: z.string(),
  // Jira
  jiraBaseUrl: z.string(),
  jiraEmail: z.string(),
  jiraApiToken: z.string(),
  jiraProjectKey: z.string(),
  jiraIssueType: z.string(),
  // Slack
  slackBotToken: z.string(),
  // PagerDuty
  pdRoutingKey: z.string(),
  // MSTeams
  teamsWebhookUrl: z.string(),
  // Telegram
  tgBotToken: z.string(),
  // Twilio
  twAccountSid: z.string(),
  twAuthToken: z.string(),
  twFromNumber: z.string(),
  // Opsgenie
  ogApiKey: z.string(),
  ogRegion: z.string(),
  // Pushover
  poAppToken: z.string(),
  // Ntfy
  ntfyServerUrl: z.string(),
  ntfyToken: z.string(),
});

export type IntegrationFormValues = z.infer<typeof integrationFormSchema>;

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
