import { useEffect, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, FormProvider, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Icon } from "@iconify/react";
import { AlertTriangle, Save, Settings } from "lucide-react";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/PageHeader";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone/DangerZone";
import { integrationsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import {
  INTEGRATION_TYPE_MAP,
  INTEGRATION_TYPES_THIRDPARTY,
  INTEGRATION_TYPES_NOTIFICATION,
  type IntegrationTypeMeta,
} from "@/constants/integrations";
import { integrationFormSchema, INTEGRATION_FORM_DEFAULTS, type IntegrationFormValues } from "../components/types";
import { GoogleCloudConfig } from "../components/GoogleCloudConfig";
import { JiraConfig } from "../components/JiraConfig";
import { PagerDutyConfig } from "../components/PagerDutyConfig";
import { MSTeamsConfig } from "../components/MSTeamsConfig";
import { TelegramConfig } from "../components/TelegramConfig";
import TwilioConfig from "../components/TwilioConfig";
import { EmailConfig } from "../components/EmailConfig";
import { OpsgenieConfig } from "../components/OpsgenieConfig";
import { PushoverConfig } from "../components/PushoverConfig";
import { NtfyConfig } from "../components/NtfyConfig";

function buildConfigJson(v: IntegrationFormValues): string {
  switch (v.type) {
    case "GoogleCloud": return JSON.stringify({ serviceAccountJson: v.serviceAccountJson.trim() });
    case "Jira": return JSON.stringify({ baseUrl: v.jiraBaseUrl.trim(), email: v.jiraEmail.trim(), apiToken: v.jiraApiToken.trim(), projectKey: v.jiraProjectKey.trim(), issueType: v.jiraIssueType.trim() });
    case "PagerDuty": return JSON.stringify({ routingKey: v.pdRoutingKey.trim() });
    case "MSTeams": return JSON.stringify({ webhookUrl: v.teamsWebhookUrl.trim() });
    case "Telegram": return JSON.stringify({ botToken: v.tgBotToken.trim() });
    case "Twilio": return JSON.stringify({ accountSid: v.twAccountSid.trim(), authToken: v.twAuthToken.trim(), fromNumber: v.twFromNumber.trim() });
    case "Opsgenie": return JSON.stringify({ apiKey: v.ogApiKey.trim(), region: v.ogRegion });
    case "Pushover": return JSON.stringify({ appToken: v.poAppToken.trim() });
    case "Ntfy": return JSON.stringify({ serverUrl: v.ntfyServerUrl.trim(), token: v.ntfyToken.trim() || undefined });
    default: return "{}";
  }
}

const NO_CONFIG_TYPES = new Set(["Email"]);

export default function IntegrationFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [searchParams] = useSearchParams();

  const providerParam = searchParams.get("provider");
  const initialType = providerParam && providerParam in INTEGRATION_TYPE_MAP ? providerParam : "Telegram";

  const methods = useForm<IntegrationFormValues>({
    resolver: zodResolver(integrationFormSchema),
    defaultValues: { ...INTEGRATION_FORM_DEFAULTS, type: initialType },
  });
  const { register, handleSubmit, control, watch, reset, formState: { errors, isSubmitting } } = methods;

  const type = watch("type");
  const [saved, setSaved] = useState(false);

  const { data: existing } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION(id!),
    queryFn: () => integrationsApi.get(Number(id)),
    enabled: isEdit,
  });

  useEffect(() => {
    if (!existing) return;
    const base: Partial<IntegrationFormValues> = { name: existing.name, description: existing.description ?? "", type: existing.type };
    try {
      const c = JSON.parse(existing.configJson);
      switch (existing.type) {
        case "GoogleCloud":
          if (!c.serviceAccountJson) base.serviceAccountJson = "";
          else {
            try { base.serviceAccountJson = JSON.stringify(JSON.parse(c.serviceAccountJson), null, 2); }
            catch { base.serviceAccountJson = c.serviceAccountJson; } // masked sentinel isn't valid JSON — keep it verbatim so an unedited submit preserves the stored key
          }
          break;
        case "Jira": base.jiraBaseUrl = c.baseUrl ?? ""; base.jiraEmail = c.email ?? ""; base.jiraApiToken = c.apiToken ?? ""; base.jiraProjectKey = c.projectKey ?? ""; base.jiraIssueType = c.issueType ?? ""; break;
        case "PagerDuty": base.pdRoutingKey = c.routingKey ?? ""; break;
        case "MSTeams": base.teamsWebhookUrl = c.webhookUrl ?? ""; break;
        case "Telegram": base.tgBotToken = c.botToken ?? ""; break;
        case "Twilio": base.twAccountSid = c.accountSid ?? ""; base.twAuthToken = c.authToken ?? ""; base.twFromNumber = c.fromNumber ?? ""; break;
        case "Opsgenie": base.ogApiKey = c.apiKey ?? ""; base.ogRegion = c.region ?? "US"; break;
        case "Pushover": base.poAppToken = c.appToken ?? ""; break;
        case "Ntfy": base.ntfyServerUrl = c.serverUrl ?? "https://ntfy.sh"; base.ntfyToken = c.token ?? ""; break;
      }
    } catch { /* ignore */ }
    reset(base as IntegrationFormValues);
  }, [existing, reset]);

  const saveMutation = useMutation({
    mutationFn: (values: IntegrationFormValues) => {
      const payload = { name: values.name, type: values.type, description: values.description || undefined, configJson: buildConfigJson(values) };
      if (isEdit) return integrationsApi.update(Number(id), payload);
      return integrationsApi.create(payload);
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS });
      if (!isEdit && data && "id" in (data as object)) {
        navigate(ROUTES.INTEGRATIONS.DETAIL((data as { id: number }).id));
        return;
      }
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => integrationsApi.delete(Number(id)),
    onSuccess: () => { qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS }); navigate(ROUTES.INTEGRATIONS.LIST); },
  });

  async function handleDelete() {
    await deleteMutation.mutateAsync();
  }

  const pageTitle = isEdit ? (existing?.name ?? "Edit Integration") : "New Integration";
  const typeMeta = INTEGRATION_TYPE_MAP[type as keyof typeof INTEGRATION_TYPE_MAP] as IntegrationTypeMeta | undefined;

  return (
    <FormProvider {...methods}>
      <form onSubmit={handleSubmit((v) => saveMutation.mutateAsync(v))}>
        <PageHeader
          breadcrumbs={[
            { label: "Integrations", onClick: () => navigate(ROUTES.INTEGRATIONS.LIST) },
            { label: pageTitle },
          ]}
          actions={
            <Button type="submit" disabled={isSubmitting || (!isEdit && NO_CONFIG_TYPES.has(type))}>
              <Save size={14} />
              {isSubmitting ? "Saving…" : saved ? "Saved!" : isEdit ? "Save changes" : "Create Integration"}
            </Button>
          }
        />

        {saveMutation.isError && (
          <div className="mb-4 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            Failed to save integration.
          </div>
        )}

        <SectionAccordion
          title="General Settings"
          description="Provider type and basic information"
          icon={<Settings size={16} className="text-muted-foreground" />}
          defaultOpen
        >
          <>
              <div className="flex flex-col gap-1.5">
                <Label>Provider</Label>
                <Controller
                  control={control}
                  name="type"
                  render={({ field }) => (
                    <Select value={field.value} onValueChange={(v) => v && field.onChange(v)} disabled={isEdit}>
                      <SelectTrigger className="w-full sm:w-56">
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
                {isEdit && <p className="text-xs text-muted-foreground">Type cannot be changed after creation.</p>}
              </div>

              {!NO_CONFIG_TYPES.has(type) && (
                <>
                  <div className="flex flex-col gap-1.5">
                    <Label>Name <span className="text-destructive">*</span></Label>
                    <Input {...register("name")} placeholder="e.g. Production Slack" />
                    {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label>Description</Label>
                    <Input {...register("description")} placeholder="Optional description" />
                  </div>
                </>
              )}
          </>
        </SectionAccordion>

        <SectionAccordion
          title="Configuration"
          description={`Credentials for ${typeMeta?.label ?? type}`}
          icon={<Icon icon={typeMeta?.icon ?? "lucide:plug"} className={`size-4 ${typeMeta?.iconClass ?? ""}`} />}
          defaultOpen
        >
          <>
            {type === "GoogleCloud" && <GoogleCloudConfig />}
            {type === "Jira" && <JiraConfig />}
            {type === "PagerDuty" && <PagerDutyConfig />}
            {type === "MSTeams" && <MSTeamsConfig />}
            {type === "Telegram" && <TelegramConfig />}
            {type === "Twilio" && <TwilioConfig />}
            {type === "Email" && <EmailConfig />}
            {type === "Opsgenie" && <OpsgenieConfig />}
            {type === "Pushover" && <PushoverConfig />}
            {type === "Ntfy" && <NtfyConfig />}
          </>
        </SectionAccordion>

        {isEdit && (
          <SectionAccordion
            title="Danger Zone"
            description="Irreversible actions for this integration"
            icon={<AlertTriangle size={16} className="text-destructive" />}
            titleClassName="text-destructive"
            disableCard
          >
            <DangerZone objectName="integration" objectId={existing?.name ?? ""} onDelete={handleDelete} />
          </SectionAccordion>
        )}
      </form>
    </FormProvider>
  );
}
