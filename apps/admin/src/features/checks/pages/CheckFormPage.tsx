import { useMemo, useRef, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Settings, Wrench, Bell } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { useCreateCheck } from "@/hooks/useChecks";
import { useService } from "@/hooks/useServices";
import { checkTypesApi } from "@/lib/actions/checks";
import { integrationsApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { PageHeader } from "@/components/PageHeader";
import { WarningConfirmDialog } from "@/components/ui/warning-confirm-dialog";
import FormActions from "@/components/ui/form-actions";
import { seedDefaults } from "@/components/config-form/seedDefaults";
import { validateConfig } from "@/components/config-form/validators";
import SchemaConfigSection from "@/features/checks/components/SchemaConfigSection";
import { CheckGeneralSettingsFields } from "@/features/checks/components/CheckGeneralSettingsFields";
import { AlertConfigListEditor, type AlertConfigListEditorHandle } from "@/features/checks/components/AlertConfigListEditor";
import type { AlertConfigDraft } from "@/features/checks/components/AlertConfigRow";
import { checkConfigSchema, type CheckConfigFormValues } from "@/features/checks/validations";
import type { components } from "@/lib/api-types";

type CheckType = components["schemas"]["CheckType"];

export default function CheckFormPage() {
  const { slug: serviceSlug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const { data: service } = useService(serviceSlug!);
  const createCheck = useCreateCheck(serviceSlug!);

  const { data: checkTypes = [] } = useQuery({
    queryKey: QUERY_KEYS.CHECK_TYPES,
    queryFn: checkTypesApi.list,
  });

  // Which integration types are actually connected — a check that requires one it doesn't have is
  // shown but disabled, so the picker is discoverable without offering an unusable choice.
  const { data: integrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });
  const connectedIntegrationTypes = useMemo(
    () => new Set(integrations.map((i) => i.type)),
    [integrations]
  );

  const [alertDrafts, setAlertDrafts] = useState<AlertConfigDraft[]>([]);
  const [submitError, setSubmitError] = useState("");
  const [configErrors, setConfigErrors] = useState<Record<string, string>>({});
  const [integrationError, setIntegrationError] = useState("");
  const [showNoAlertsWarning, setShowNoAlertsWarning] = useState(false);
  const [pendingValues, setPendingValues] = useState<CheckConfigFormValues | null>(null);
  const alertConfigEditorRef = useRef<AlertConfigListEditorHandle>(null);

  const methods = useForm<CheckConfigFormValues>({
    resolver: zodResolver(checkConfigSchema),
    defaultValues: {
      name: "",
      slug: "",
      description: "",
      cron: "* * * * *",
      showCustomCron: false,
      isActive: true,
      isMultiRegion: false,
      type: "HTTP",
      config: {},
      integrationId: "",
    },
  });

  const { watch, setValue, handleSubmit } = methods;
  const type = watch("type") as CheckType;

  const typeMeta = useMemo(() => checkTypes.find((t) => t.type === type), [checkTypes, type]);

  // Seed the config defaults for the initially-selected type once its manifest arrives.
  const seededFor = useRef<string | null>(null);
  if (typeMeta && seededFor.current !== type) {
    seededFor.current = type;
    setValue("config", seedDefaults(typeMeta.configSchema));
  }

  function handleTypeChange(t: CheckType) {
    setValue("type", t);
    const meta = checkTypes.find((ct) => ct.type === t);
    seededFor.current = t;
    setValue("config", meta ? seedDefaults(meta.configSchema) : {});
    setConfigErrors({});
  }

  async function createTheCheck(values: CheckConfigFormValues) {
    setSubmitError("");
    try {
      const check = await createCheck.mutateAsync({
        slug: values.slug,
        name: values.name,
        description: values.description || null,
        type: values.type as CheckType,
        cron: values.cron,
        typeDataJson: JSON.stringify(values.config ?? {}),
        isActive: values.isActive,
        isMultiRegion: values.isMultiRegion,
        integrationId: values.integrationId || undefined,
        alertConfigs: alertDrafts,
      });
      navigate(ROUTES.CHECKS.DETAIL(serviceSlug!, check.slug));
    } catch (err: unknown) {
      setSubmitError(err instanceof Error ? err.message : "Failed to create check.");
    }
  }

  async function onSubmit(values: CheckConfigFormValues) {
    const config = (values.config ?? {}) as Record<string, unknown>;
    // The integration-instance field is validated via the picker below, not the generic schema form
    // (it's hidden there), so exclude it to avoid a duplicate error on a field the user can't see.
    const schemaForValidation = (typeMeta?.configSchema ?? []).filter(
      (f) => !typeMeta?.requiredIntegrationType || f.key !== "integrationInstanceId"
    );
    const errors = validateConfig(schemaForValidation, config);
    setConfigErrors(errors);
    // The required integration is now stored in the check's config (config.integrationInstanceId),
    // which is what the check actually reads — not the legacy Check.integrationId field.
    const missingIntegration = !!typeMeta?.requiredIntegrationType && !config.integrationInstanceId;
    setIntegrationError(missingIntegration ? `A ${typeMeta!.requiredIntegrationType} integration is required.` : "");
    if (Object.keys(errors).length > 0 || missingIntegration) {
      setSubmitError("Fix the highlighted configuration fields before creating this check.");
      return;
    }

    const alertConfigsValid = await alertConfigEditorRef.current?.validateAll() ?? true;
    if (!alertConfigsValid) {
      setSubmitError("Fix the invalid alert configuration(s) before creating this check.");
      return;
    }
    if (alertDrafts.length === 0) {
      setPendingValues(values);
      setShowNoAlertsWarning(true);
      return;
    }
    await createTheCheck(values);
  }

  async function handleConfirmCreateWithoutAlerts() {
    if (!pendingValues) return;
    setShowNoAlertsWarning(false);
    await createTheCheck(pendingValues);
    setPendingValues(null);
  }

  const typeSelect = (
    <Select value={type} onValueChange={(v) => v && handleTypeChange(v as CheckType)}>
      <SelectTrigger className="w-full">
        <SelectValue>{(v: string) => checkTypes.find((t) => t.type === v)?.displayName ?? v}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {checkTypes.filter((t) => t.hasExecutor).map((t) => {
          // A check that requires a provider integration is only selectable once one is connected —
          // otherwise it's shown disabled with a tooltip explaining what to connect first.
          const missingIntegration =
            !!t.requiredIntegrationType && !connectedIntegrationTypes.has(t.requiredIntegrationType);

          if (missingIntegration) {
            return (
              <Tooltip key={t.type}>
                <TooltipTrigger
                  // A disabled SelectItem swallows pointer events, so wrap it in a span the tooltip can hover.
                  render={
                    <span className="block">
                      <SelectItem value={t.type} disabled>{t.displayName}</SelectItem>
                    </span>
                  }
                />
                <TooltipContent side="right">
                  Requires a {t.requiredIntegrationType} integration. Connect one first.
                </TooltipContent>
              </Tooltip>
            );
          }

          return <SelectItem key={t.type} value={t.type}>{t.displayName}</SelectItem>;
        })}
      </SelectContent>
    </Select>
  );

  return (
    <>
      <FormProvider {...methods}>
        <form onSubmit={handleSubmit(onSubmit)}>
          <PageHeader
            breadcrumbs={[
              { label: "Services", onClick: () => navigate(ROUTES.SERVICES.LIST) },
              { label: service?.name ?? serviceSlug!, onClick: () => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!)) },
              { label: "New Check" },
            ]}
          />

          {submitError && (
            <div className="mb-4 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
              {submitError}
            </div>
          )}

          <SectionAccordion
            title="General Settings"
            description="Basic information about this check"
            icon={<Settings size={16} className="text-muted-foreground" />}
            defaultOpen
          >
            <CheckGeneralSettingsFields typeNode={typeSelect} slugEditable />
          </SectionAccordion>

          <SectionAccordion
            title="Configuration"
            description={`Settings for the ${typeMeta?.displayName ?? type} check`}
            icon={<Wrench size={16} className="text-muted-foreground" />}
            defaultOpen
          >
            <SchemaConfigSection typeMeta={typeMeta} errors={configErrors} integrationError={integrationError} />
          </SectionAccordion>

          <SectionAccordion
            title="Alert Configurations"
            description="Notification channels triggered by this check"
            icon={<Bell size={16} className="text-muted-foreground" />}
            disableCard
          >
            <AlertConfigListEditor ref={alertConfigEditorRef} dimensions={typeMeta?.dimensions ?? []} value={alertDrafts} onChange={setAlertDrafts} />
          </SectionAccordion>

          <FormActions
            onCancel={() => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!))}
            submitLabel="Create Check"
            submitPendingLabel="Creating…"
            submitIcon={<Settings size={14} />}
            isPending={createCheck.isPending}
          />
        </form>
      </FormProvider>

      <WarningConfirmDialog
        open={showNoAlertsWarning}
        onOpenChange={setShowNoAlertsWarning}
        title="Create check without any alerts?"
        description={
          <>
            This check has no Alert Configurations. It will still run and report its status, but no one
            will be notified if it goes down. You can add alert configurations later from the check's page.
          </>
        }
        confirmLabel="Create anyway"
        confirmPendingLabel="Creating…"
        onConfirm={handleConfirmCreateWithoutAlerts}
        isPending={createCheck.isPending}
      />
    </>
  );
}
