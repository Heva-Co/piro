import { useEffect, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, Controller } from "react-hook-form";
import { Icon } from "@iconify/react";
import { AdminLayout } from "@/components/AdminLayout";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { integrationsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import {
  INTEGRATION_TYPE_MAP,
  INTEGRATION_TYPES_THIRDPARTY,
  INTEGRATION_TYPES_NOTIFICATION,
} from "@/constants/integrations";
import { GoogleCloudConfig } from "../components/GoogleCloudConfig";
import { JiraConfig } from "../components/JiraConfig";

interface FormValues {
  name: string;
  description: string;
  type: string;
  // GoogleCloud
  serviceAccountJson: string;
  // Jira
  jiraBaseUrl: string;
  jiraEmail: string;
  jiraApiToken: string;
  jiraProjectKey: string;
  jiraIssueType: string;
  // Webhook
  whUrl: string;
  whSecret: string;
  // Email (SMTP)
  smtpHost: string;
  smtpPort: string;
  smtpUser: string;
  smtpPassword: string;
  smtpFrom: string;
  smtpTls: boolean;
  // Slack
  slackBotToken: string;
  // PagerDuty
  pdRoutingKey: string;
  // MSTeams
  teamsWebhookUrl: string;
  // Telegram
  tgBotToken: string;
  // Twilio
  twAccountSid: string;
  twAuthToken: string;
  twFromNumber: string;
  // Google Chat
  gcWebhookUrl: string;
  // Discord
  discordWebhookUrl: string;
  // Opsgenie
  ogApiKey: string;
  ogRegion: string;
  // Pushover
  poAppToken: string;
  // Ntfy
  ntfyServerUrl: string;
  ntfyToken: string;
}

const DEFAULT_VALUES: FormValues = {
  name: "", description: "", type: "Slack",
  serviceAccountJson: "",
  jiraBaseUrl: "", jiraEmail: "", jiraApiToken: "", jiraProjectKey: "", jiraIssueType: "",
  whUrl: "", whSecret: "",
  smtpHost: "", smtpPort: "587", smtpUser: "", smtpPassword: "", smtpFrom: "", smtpTls: true,
  slackBotToken: "",
  pdRoutingKey: "",
  teamsWebhookUrl: "",
  tgBotToken: "",
  twAccountSid: "", twAuthToken: "", twFromNumber: "",
  gcWebhookUrl: "",
  discordWebhookUrl: "",
  ogApiKey: "", ogRegion: "US",
  poAppToken: "",
  ntfyServerUrl: "https://ntfy.sh", ntfyToken: "",
};

const inp = "rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";
const label = "text-sm font-semibold";

export default function IntegrationFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [searchParams] = useSearchParams();

  const providerParam = searchParams.get("provider");
  const initialType = providerParam && providerParam in INTEGRATION_TYPE_MAP ? providerParam : "Slack";

  const { register, handleSubmit, control, watch, reset, formState: { errors, isSubmitting } } =
    useForm<FormValues>({ defaultValues: { ...DEFAULT_VALUES, type: initialType } });

  const type = watch("type");
  const [deleteConfirm, setDeleteConfirm] = useState("");

  const { data: existing } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION(id!),
    queryFn: () => integrationsApi.get(Number(id)),
    enabled: isEdit,
  });

  useEffect(() => {
    if (!existing) return;
    const base: Partial<FormValues> = { name: existing.name, description: existing.description ?? "", type: existing.type };
    try {
      const c = JSON.parse(existing.configJson);
      switch (existing.type) {
        case "GoogleCloud": base.serviceAccountJson = c.serviceAccountJson ? JSON.stringify(JSON.parse(c.serviceAccountJson), null, 2) : ""; break;
        case "Jira": base.jiraBaseUrl = c.baseUrl ?? ""; base.jiraEmail = c.email ?? ""; base.jiraApiToken = c.apiToken ?? ""; base.jiraProjectKey = c.projectKey ?? ""; base.jiraIssueType = c.issueType ?? ""; break;
        case "Webhook": base.whUrl = c.url ?? ""; base.whSecret = c.secret ?? ""; break;
        case "Email": base.smtpHost = c.host ?? ""; base.smtpPort = String(c.port ?? 587); base.smtpUser = c.username ?? ""; base.smtpPassword = c.password ?? ""; base.smtpFrom = c.from ?? ""; base.smtpTls = c.tls ?? true; break;
        case "Slack": base.slackBotToken = c.botToken ?? ""; break;
        case "PagerDuty": base.pdRoutingKey = c.routingKey ?? ""; break;
        case "MSTeams": base.teamsWebhookUrl = c.webhookUrl ?? ""; break;
        case "Telegram": base.tgBotToken = c.botToken ?? ""; break;
        case "TwilioSms": base.twAccountSid = c.accountSid ?? ""; base.twAuthToken = c.authToken ?? ""; base.twFromNumber = c.fromNumber ?? ""; break;
        case "GoogleChat": base.gcWebhookUrl = c.webhookUrl ?? ""; break;
        case "Discord": base.discordWebhookUrl = c.webhookUrl ?? ""; break;
        case "Opsgenie": base.ogApiKey = c.apiKey ?? ""; base.ogRegion = c.region ?? "US"; break;
        case "Pushover": base.poAppToken = c.appToken ?? ""; break;
        case "Ntfy": base.ntfyServerUrl = c.serverUrl ?? "https://ntfy.sh"; base.ntfyToken = c.token ?? ""; break;
      }
    } catch { /* ignore */ }
    reset(base as FormValues);
  }, [existing, reset]);

  function buildConfigJson(v: FormValues): string {
    switch (v.type) {
      case "GoogleCloud": return JSON.stringify({ serviceAccountJson: v.serviceAccountJson.trim() });
      case "Jira": return JSON.stringify({ baseUrl: v.jiraBaseUrl.trim(), email: v.jiraEmail.trim(), apiToken: v.jiraApiToken.trim(), projectKey: v.jiraProjectKey.trim(), issueType: v.jiraIssueType.trim() });
      case "Webhook": return JSON.stringify({ url: v.whUrl.trim(), secret: v.whSecret.trim() || undefined });
      case "Email": return JSON.stringify({ host: v.smtpHost.trim(), port: Number(v.smtpPort), username: v.smtpUser.trim(), password: v.smtpPassword.trim(), from: v.smtpFrom.trim(), tls: v.smtpTls });
      case "Slack": return JSON.stringify({ botToken: v.slackBotToken.trim() });
      case "PagerDuty": return JSON.stringify({ routingKey: v.pdRoutingKey.trim() });
      case "MSTeams": return JSON.stringify({ webhookUrl: v.teamsWebhookUrl.trim() });
      case "Telegram": return JSON.stringify({ botToken: v.tgBotToken.trim() });
      case "TwilioSms": return JSON.stringify({ accountSid: v.twAccountSid.trim(), authToken: v.twAuthToken.trim(), fromNumber: v.twFromNumber.trim() });
      case "GoogleChat": return JSON.stringify({ webhookUrl: v.gcWebhookUrl.trim() });
      case "Discord": return JSON.stringify({ webhookUrl: v.discordWebhookUrl.trim() });
      case "Opsgenie": return JSON.stringify({ apiKey: v.ogApiKey.trim(), region: v.ogRegion });
      case "Pushover": return JSON.stringify({ appToken: v.poAppToken.trim() });
      case "Ntfy": return JSON.stringify({ serverUrl: v.ntfyServerUrl.trim(), token: v.ntfyToken.trim() || undefined });
      default: return "{}";
    }
  }

  const saveMutation = useMutation({
    mutationFn: (values: FormValues) => {
      const payload = { name: values.name, type: values.type, description: values.description || undefined, configJson: buildConfigJson(values) };
      if (isEdit) return integrationsApi.update(Number(id), payload);
      return integrationsApi.create(payload);
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS });
      if (!isEdit && data && "id" in (data as object)) {
        navigate(ROUTES.INTEGRATIONS.DETAIL((data as { id: number }).id));
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => integrationsApi.delete(Number(id)),
    onSuccess: () => { qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS }); navigate(ROUTES.INTEGRATIONS.LIST); },
  });

  const pageTitle = isEdit ? (existing?.name ?? "Edit Integration") : "New Integration";
  const typeMeta = INTEGRATION_TYPE_MAP[type as keyof typeof INTEGRATION_TYPE_MAP];

  return (
    <AdminLayout title={pageTitle}>
      <div className="flex flex-col gap-6">
        <nav className="flex items-center gap-2 text-sm text-muted-foreground">
          <button onClick={() => navigate(ROUTES.INTEGRATIONS.LIST)} className="hover:text-foreground transition-colors">
            Integrations
          </button>
          <span>/</span>
          <span className="text-foreground font-medium">{pageTitle}</span>
        </nav>

        <div>
          <h1 className="text-xl font-bold">{pageTitle}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Shared credentials reused across channels and checks.</p>
        </div>

        {saveMutation.isError && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            Failed to save integration.
          </div>
        )}

        <form onSubmit={handleSubmit((v) => saveMutation.mutateAsync(v))}>
          <div className="rounded-xl border bg-card">
            {/* Type selector */}
            <div className="px-6 pt-6 pb-4 border-b border-border">
              <p className="text-sm font-semibold mb-1">Provider</p>
              <p className="text-xs text-muted-foreground mb-3">Select the integration type</p>
              <Controller
                control={control}
                name="type"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={(v) => v && field.onChange(v)} disabled={isEdit}>
                    <SelectTrigger className="w-56">
                      <SelectValue>
                        <span className="inline-flex items-center gap-2">
                          {typeMeta?.icon && <Icon icon={typeMeta.icon} className={`size-4 ${typeMeta.iconClass ?? ""}`} />}
                          {typeMeta?.label ?? field.value}
                        </span>
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      <div className="px-2 py-1 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                        Third-party
                      </div>
                      {INTEGRATION_TYPES_THIRDPARTY.map((t) => (
                        <SelectItem key={t.value} value={t.value} disabled={t.upcoming}>
                          <span className="inline-flex items-center gap-2">
                            <Icon icon={t.icon} className={`size-4 ${t.iconClass ?? ""} ${t.upcoming ? "opacity-40" : ""}`} />
                            <span className={t.upcoming ? "opacity-40" : ""}>{t.label}</span>
                            {t.upcoming && <span className="ml-auto rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground">Soon</span>}
                          </span>
                        </SelectItem>
                      ))}
                      <div className="px-2 py-1 mt-1 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground border-t border-border">
                        Notification
                      </div>
                      {INTEGRATION_TYPES_NOTIFICATION.map((t) => (
                        <SelectItem key={t.value} value={t.value}>
                          <span className="inline-flex items-center gap-2">
                            <Icon icon={t.icon} className={`size-4 ${t.iconClass ?? ""}`} />
                            {t.label}
                            {t.alpha && (
                              <span className="ml-auto rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400 px-1.5 py-0.5 text-[10px] font-medium">
                                Alpha
                              </span>
                            )}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {isEdit && <p className="text-xs text-muted-foreground mt-1.5">Type cannot be changed after creation.</p>}
            </div>

            {/* Common + type-specific fields */}
            <div className="px-6 py-6 flex flex-col gap-5">
              <div className="flex flex-col gap-1.5">
                <label className={label}>Name <span className="text-destructive">*</span></label>
                <input {...register("name", { required: "Name is required" })} placeholder="e.g. Production Slack" className={inp} />
                {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
              </div>
              <div className="flex flex-col gap-1.5">
                <label className={label}>Description</label>
                <input {...register("description")} placeholder="Optional description" className={inp} />
              </div>

              {/* Third-party */}
              {type === "GoogleCloud" && (
                <Controller control={control} name="serviceAccountJson" rules={{ required: "Required" }}
                  render={({ field }) => (
                    <div className="flex flex-col gap-1">
                      <GoogleCloudConfig serviceAccountJson={field.value} setServiceAccountJson={field.onChange} />
                      {errors.serviceAccountJson && <p className="text-xs text-destructive">{errors.serviceAccountJson.message}</p>}
                    </div>
                  )}
                />
              )}
              {type === "Jira" && (
                <JiraConfig baseUrl={watch("jiraBaseUrl")} email={watch("jiraEmail")} apiToken={watch("jiraApiToken")} projectKey={watch("jiraProjectKey")} issueType={watch("jiraIssueType")}
                  errors={{ baseUrl: errors.jiraBaseUrl?.message, email: errors.jiraEmail?.message, apiToken: errors.jiraApiToken?.message }} register={register} />
              )}

              {/* Notification */}
              {type === "Webhook" && <>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Webhook URL <span className="text-destructive">*</span></label>
                  <input type="url" {...register("whUrl", { required: "Required" })} placeholder="https://example.com/webhook" className={inp} />
                  {errors.whUrl && <p className="text-xs text-destructive">{errors.whUrl.message}</p>}
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Secret</label>
                  <input {...register("whSecret")} placeholder="HMAC-SHA256 signing secret (optional)" className={inp} />
                </div>
              </>}


              {type === "Slack" && (
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Bot Token <span className="text-destructive">*</span></label>
                  <input {...register("slackBotToken", { required: "Required" })} placeholder="xoxb-…" className={inp} />
                  {errors.slackBotToken && <p className="text-xs text-destructive">{errors.slackBotToken.message}</p>}
                  <p className="text-xs text-muted-foreground">OAuth Bot Token from your Slack app. Requires <code className="font-mono">chat:write</code> scope.</p>
                </div>
              )}

              {type === "PagerDuty" && (
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Routing Key <span className="text-destructive">*</span></label>
                  <input {...register("pdRoutingKey", { required: "Required" })} placeholder="PagerDuty Events API v2 routing key" className={inp} />
                  {errors.pdRoutingKey && <p className="text-xs text-destructive">{errors.pdRoutingKey.message}</p>}
                </div>
              )}

              {type === "MSTeams" && (
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Incoming Webhook URL <span className="text-destructive">*</span></label>
                  <input type="url" {...register("teamsWebhookUrl", { required: "Required" })} placeholder="https://outlook.office.com/webhook/…" className={inp} />
                  {errors.teamsWebhookUrl && <p className="text-xs text-destructive">{errors.teamsWebhookUrl.message}</p>}
                </div>
              )}

              {type === "Telegram" && (
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Bot Token <span className="text-destructive">*</span></label>
                  <input {...register("tgBotToken", { required: "Required" })} placeholder="123456:ABC-DEF…" className={inp} />
                  {errors.tgBotToken && <p className="text-xs text-destructive">{errors.tgBotToken.message}</p>}
                  <p className="text-xs text-muted-foreground">From @BotFather. Each channel using this integration provides its own Chat ID.</p>
                </div>
              )}

              {type === "TwilioSms" && <>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Account SID <span className="text-destructive">*</span></label>
                  <input {...register("twAccountSid", { required: "Required" })} className={inp} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Auth Token <span className="text-destructive">*</span></label>
                  <input type="password" {...register("twAuthToken", { required: "Required" })} className={inp} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>From number <span className="text-destructive">*</span></label>
                  <input {...register("twFromNumber", { required: "Required" })} placeholder="+15005550006" className={inp} />
                </div>
              </>}

              {type === "GoogleChat" && (
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Webhook URL <span className="text-destructive">*</span></label>
                  <input type="url" {...register("gcWebhookUrl", { required: "Required" })} className={inp} />
                  {errors.gcWebhookUrl && <p className="text-xs text-destructive">{errors.gcWebhookUrl.message}</p>}
                </div>
              )}

              {type === "Discord" && (
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Webhook URL <span className="text-destructive">*</span></label>
                  <input type="url" {...register("discordWebhookUrl", { required: "Required" })} className={inp} />
                  {errors.discordWebhookUrl && <p className="text-xs text-destructive">{errors.discordWebhookUrl.message}</p>}
                </div>
              )}

              {type === "Opsgenie" && <>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>API Key <span className="text-destructive">*</span></label>
                  <input {...register("ogApiKey", { required: "Required" })} className={inp} />
                  {errors.ogApiKey && <p className="text-xs text-destructive">{errors.ogApiKey.message}</p>}
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Region</label>
                  <select {...register("ogRegion")} className={inp}>
                    <option value="US">US</option>
                    <option value="EU">EU</option>
                  </select>
                </div>
              </>}

              {type === "Pushover" && (
                <div className="flex flex-col gap-1.5">
                  <label className={label}>App Token <span className="text-destructive">*</span></label>
                  <input {...register("poAppToken", { required: "Required" })} className={inp} />
                  {errors.poAppToken && <p className="text-xs text-destructive">{errors.poAppToken.message}</p>}
                  <p className="text-xs text-muted-foreground">Each user provides their own User Key when configuring a channel.</p>
                </div>
              )}

              {type === "Ntfy" && <>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Server URL <span className="text-destructive">*</span></label>
                  <input type="url" {...register("ntfyServerUrl", { required: "Required" })} className={inp} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className={label}>Access Token</label>
                  <input {...register("ntfyToken")} placeholder="Optional — required for protected topics" className={inp} />
                </div>
              </>}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-border">
              <button type="button" onClick={() => navigate(ROUTES.INTEGRATIONS.LIST)}
                className="rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted transition-colors">
                Cancel
              </button>
              <button type="submit" disabled={isSubmitting}
                className="rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity">
                {isSubmitting ? "Saving…" : isEdit ? "Save changes" : "Create Integration"}
              </button>
            </div>
          </div>
        </form>

        {isEdit && (
          <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6 flex flex-col gap-4">
            <p className="text-sm">
              Permanently delete this integration. Type{" "}
              <code className="font-mono font-semibold">{existing?.name}</code> to confirm.
            </p>
            <p className="text-xs text-muted-foreground">
              Deletion is blocked if any checks or channels are still referencing this integration.
            </p>
            <div className="flex items-center gap-3">
              <input value={deleteConfirm} onChange={(e) => setDeleteConfirm(e.target.value)}
                placeholder={existing?.name ?? ""}
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-destructive w-64" />
              <button type="button" disabled={deleteConfirm !== existing?.name || deleteMutation.isPending}
                onClick={() => deleteMutation.mutate()}
                className="rounded-lg bg-destructive text-destructive-foreground px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-40 transition-opacity">
                {deleteMutation.isPending ? "Deleting…" : "Delete Integration"}
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
