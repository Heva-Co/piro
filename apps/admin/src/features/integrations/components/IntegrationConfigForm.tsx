import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQueryClient, useMutation } from "@tanstack/react-query";
import { useForm, Controller } from "react-hook-form";
import { Icon } from "@iconify/react";
import { AlertTriangle, Save, Settings, Webhook } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/PageHeader";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone/DangerZone";
import { integrationsApi, type Integration, type IntegrationTypeMeta, type CreateIntegrationRequest } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { MASKED_SECRET_VALUE } from "@/constants/integrations";
import { DynamicConfigField } from "./DynamicConfigField";
import { IntegrationWebhookUrlField } from "./IntegrationWebhookUrlField";
import { WebhookRequestLogViewer } from "./WebhookRequestLogViewer";
import WebhookRequestLogActions from "./WebhookRequestLogActions";
import { IntegrationEscalationPolicyField } from "./IntegrationEscalationPolicyField";

interface GeneralFormValues {
  name: string;
  description: string;
  escalationPolicyId: number | null;
}

/** Parses an Integration's ConfigJson into the flat string-map DynamicConfigField expects. */
function parseConfigJson(configJson: string): Record<string, string> {
  const values: Record<string, string> = {};
  try {
    const parsed = JSON.parse(configJson) as Record<string, unknown>;
    for (const [key, val] of Object.entries(parsed))
      values[key] = val == null ? "" : String(val);
  } catch { /* malformed ConfigJson — fall back to an empty config */ }
  return values;
}

interface Props {
  /** Integration id in edit mode; undefined when creating a new one. */
  id?: string;
  /** The already-loaded Integration in edit mode; undefined when creating a new one. */
  existing?: Integration;
  /** The integration type this form configures — fixed for the lifetime of this component. */
  resolvedType: string;
  /** The resolved type's manifest, used to render Configuration fields and the provider badge. */
  typeMeta?: IntegrationTypeMeta;
  /** Called when the user backs out of type selection (create mode only). */
  onBack?: () => void;
}

/**
 * Renders the "General Settings" + "Configuration" (+ "Danger Zone" in edit mode) sections.
 * Assumes its inputs (`existing`, `typeMeta`) are already loaded — the parent (IntegrationConfigStep)
 * is responsible for waiting on those queries so this component's initial state can be derived
 * directly from them, rather than synchronized in via a post-mount effect.
 */
export function IntegrationConfigForm(props: Props) {
  const { id, existing, resolvedType, typeMeta, onBack } = props;
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const qc = useQueryClient();

  const { register, handleSubmit, control, formState: { errors, isSubmitting } } = useForm<GeneralFormValues>({
    defaultValues: {
      name: existing?.name ?? "",
      description: existing?.description ?? "",
      escalationPolicyId: existing?.escalationPolicyId ?? null,
    },
    mode: "all"
  });
  const [configValues, setConfigValues] = useState<Record<string, string>>(() =>
    existing ? parseConfigJson(existing.configJson) : {}
  );
  const [configErrors, setConfigErrors] = useState<Record<string, string>>({});
  const [saved, setSaved] = useState(false);
  /** Set once, right after creation — holds the response with any generated field unmasked (see GeneratedConfigField). Navigation to the detail page waits until the admin dismisses this. */
  const [createdIntegration, setCreatedIntegration] = useState<Integration | null>(null);

  const saveMutation = useMutation({
    mutationFn: (values: GeneralFormValues) => {
      const configJson = JSON.stringify(configValues);
      const description = values.description || null;
      const escalationPolicyId = values.escalationPolicyId;
      if (isEdit) return integrationsApi.update(id!, { name: values.name, description, configJson, escalationPolicyId });
      return integrationsApi.create({ name: values.name, type: resolvedType as CreateIntegrationRequest["type"], description, configJson, escalationPolicyId });
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS });
      if (!isEdit && data && "id" in (data as object)) {
        // The create response is the one time a server-generated field (e.g. a webhook auth
        // token) comes back unmasked — stash it so the confirmation view can show/copy it.
        setConfigValues(parseConfigJson(data.configJson));
        setCreatedIntegration(data);
        return;
      }
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => integrationsApi.delete(id!),
    onSuccess: () => { qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS }); navigate(ROUTES.INTEGRATIONS.LIST); },
  });

  const regenerateMutation = useMutation({
    mutationFn: () => integrationsApi.regenerateGeneratedFields(id!),
    onSuccess: (data) => {
      // Same one-time-reveal shape as the create response — the new value is only visible here.
      setConfigValues(parseConfigJson(data.configJson));
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS });
    },
  });

  async function handleDelete() {
    await deleteMutation.mutateAsync();
  }

  function submit(values: GeneralFormValues) {
    const nextErrors: Record<string, string> = {};
    for (const field of typeMeta?.configSchema ?? []) {
      if (field.isGenerated) continue; // the server fills this in — nothing to validate here
      const value = configValues[field.key] ?? "";
      const isMasked = field.isSecret && value === MASKED_SECRET_VALUE;
      if (field.required && !isMasked && value.trim() === "") {
        nextErrors[field.key] = `${field.label} is required`;
      }
    }
    setConfigErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    saveMutation.mutate(values);
  }

  function handleContinue() {
    if (createdIntegration) navigate(ROUTES.INTEGRATIONS.DETAIL(createdIntegration.id));
  }

  const pageTitle = isEdit ? (existing?.name ?? "Edit Integration") : "New Integration";
  const hasConfigFields = (typeMeta?.configSchema.length ?? 0) > 0;

  return (
    <form onSubmit={handleSubmit(submit)}>
      <PageHeader
        breadcrumbs={[
          { label: "Integrations", onClick: () => navigate(ROUTES.INTEGRATIONS.LIST) },
          ...(!isEdit && onBack ? [{ label: "Choose provider", onClick: onBack }] : []),
          { label: pageTitle },
        ]}
        actions={
          createdIntegration ? (
            <Button type="button" onClick={handleContinue}>Continue</Button>
          ) : (
            <Button type="submit" disabled={isSubmitting}>
              <Save size={14} />
              {isSubmitting ? "Saving…" : saved ? "Saved!" : isEdit ? "Save changes" : "Create Integration"}
            </Button>
          )
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
            <div className="inline-flex w-fit items-center gap-2 rounded-lg border bg-muted/40 px-3 py-2 text-sm font-medium">
              {typeMeta?.iconifyIcon && <Icon icon={typeMeta.iconifyIcon} className="size-4" />}
              {typeMeta?.label ?? resolvedType}
            </div>
            <p className="text-xs text-muted-foreground">
              {isEdit ? "Type cannot be changed after creation." : "Go back to choose a different provider."}
            </p>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label>Name <span className="text-destructive">*</span></Label>
            <Input {...register("name", { required: "Name is required" })} placeholder="e.g. Production Slack" />
            {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
          </div>
          <div className="flex flex-col gap-1.5">
            <Label>Description</Label>
            <Input {...register("description")} placeholder="Optional description" />
          </div>

          {typeMeta?.capabilities.includes("SupportsEscalationPolicy") && (
            <Controller
              name="escalationPolicyId"
              control={control}
              render={({ field }) => (
                <IntegrationEscalationPolicyField value={field.value} onChange={field.onChange} />
              )}
            />
          )}
        </>
      </SectionAccordion>

      {hasConfigFields && (
        <SectionAccordion
          title="Configuration"
          description={`Credentials for ${typeMeta?.label ?? resolvedType}`}
          icon={<Icon icon={typeMeta?.iconifyIcon ?? "lucide:plug"} className="size-4" />}
          defaultOpen
        >
          <>
            {createdIntegration && (
              <p className="text-sm text-muted-foreground">
                Integration created. Save any generated credentials below before continuing.
              </p>
            )}
            {typeMeta?.configSchema.map((field) => (
              <DynamicConfigField
                key={field.key}
                field={field}
                value={configValues[field.key] ?? ""}
                error={configErrors[field.key]}
                onChange={(v) => setConfigValues((prev) => ({ ...prev, [field.key]: v }))}
                isCreating={!isEdit && !createdIntegration}
                onRegenerate={isEdit ? () => regenerateMutation.mutate() : undefined}
                isRegenerating={regenerateMutation.isPending}
              />
            ))}
            {typeMeta && (id || createdIntegration) && (
              <IntegrationWebhookUrlField
                integrationId={id ?? createdIntegration!.id}
                typeMeta={typeMeta}
                configValues={configValues}
              />
            )}
          </>
        </SectionAccordion>
      )}

      {isEdit && typeMeta?.webhookPath && (
        <SectionAccordion
          title="Webhook Requests"
          description="Recent inbound requests to this integration's webhook"
          icon={<Webhook size={16} className="text-muted-foreground" />}
          actions={<WebhookRequestLogActions integrationId={id!} />}
          disableCard
        >
          <WebhookRequestLogViewer integrationId={id!} />
        </SectionAccordion>
      )}

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
  );
}
