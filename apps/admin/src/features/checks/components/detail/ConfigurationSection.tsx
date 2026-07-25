import { useEffect, useRef, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Save } from "lucide-react";
import { useCheck, useUpdateCheck, useCheckTypeMeta } from "@/hooks/useChecks";
import DynamicConfigForm from "@/components/config-form/DynamicConfigForm";
import { seedFromTypeData } from "@/components/config-form/seedDefaults";
import { validateConfig } from "@/components/config-form/validators";
import RequiredIntegrationPicker from "@/features/checks/components/shared/RequiredIntegrationPicker";
import ScriptTestPanel from "@/features/checks/components/detail/ScriptTestPanel";
import HeartbeatPanel from "@/features/checks/components/detail/HeartbeatPanel";
import { Button } from "@/components/ui/button";

interface Props {
  serviceSlug: string;
  checkSlug: string;
}

// The per-type config is an opaque structured object, held as a single `config` field so its own
// validation stays schema-driven (validateConfig / RFC 0011) — same shape CheckFormPage uses. RHF
// only hosts it, which gives us formState.isDirty for free.
interface ConfigFormValues {
  config: Record<string, unknown>;
}

function ConfigurationSection(props: Props) {
  const { serviceSlug, checkSlug } = props;

  const { data: check } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const typeMeta = useCheckTypeMeta(check?.type);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");
  const [configErrors, setConfigErrors] = useState<Record<string, string>>({});
  const [integrationError, setIntegrationError] = useState("");

  const { control, setValue, reset, handleSubmit, formState } = useForm<ConfigFormValues>({
    defaultValues: { config: {} },
  });
  const configValues = useWatch({ control, name: "config" }) ?? {};

  const requiredIntegration = typeMeta?.requiredIntegrationType;

  // The required integration lives inside the check's config (config.integrationInstanceId) — what the
  // check actually reads — so the picker reads/writes it there, and it's hidden from the schema form.
  const integrationInstanceId = (configValues.integrationInstanceId as string) ?? "";

  // Seed the config once both the check and its type manifest are available. reset() makes this the
  // dirty baseline, so Save stays disabled until the user actually changes something.
  const seeded = useRef(false);
  useEffect(() => {
    if (check && typeMeta && !seeded.current) {
      seeded.current = true;
      reset({ config: seedFromTypeData(typeMeta.configSchema, check.typeDataJson) });
    }
  }, [check, typeMeta, reset]);

  function setConfig(next: Record<string, unknown>) {
    setValue("config", next, { shouldDirty: true });
  }

  async function handleSave(values: ConfigFormValues) {
    setError("");
    const config = values.config;
    // The integration-instance field is validated via the picker, not the generic schema form (hidden there).
    const schemaForValidation = (typeMeta?.configSchema ?? []).filter(
      (f) => !requiredIntegration || f.key !== "integrationInstanceId"
    );
    const errors = validateConfig(schemaForValidation, config);
    setConfigErrors(errors);
    const missingIntegration = !!requiredIntegration && !config.integrationInstanceId;
    setIntegrationError(missingIntegration ? `A ${requiredIntegration} integration is required.` : "");
    if (Object.keys(errors).length > 0 || missingIntegration) {
      setError("Fix the highlighted configuration fields before saving.");
      return;
    }
    try {
      await updateCheck.mutateAsync({ typeDataJson: JSON.stringify(config) });
      reset({ config });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save.");
    }
  }

  const schema = typeMeta?.configSchema ?? [];
  const visibleSchema = requiredIntegration ? schema.filter((f) => f.key !== "integrationInstanceId") : schema;

  return (
    <form onSubmit={handleSubmit(handleSave)} className="flex flex-col gap-5">
      {error && (
        <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">{error}</div>
      )}

      {requiredIntegration && (
        <RequiredIntegrationPicker
          integrationType={requiredIntegration}
          value={integrationInstanceId}
          onChange={(id) => setConfig({ ...configValues, integrationInstanceId: id })}
          error={integrationError}
        />
      )}

      {schema.length === 0 && !requiredIntegration ? (
        <p className="text-sm text-muted-foreground">This check type has no configuration.</p>
      ) : visibleSchema.length > 0 ? (
        <DynamicConfigForm schema={visibleSchema} values={configValues} errors={configErrors} onChange={setConfig} />
      ) : null}

      {check?.type === "Script" && (
        <div className="border-t pt-4">
          <ScriptTestPanel
            serviceSlug={serviceSlug}
            checkSlug={checkSlug}
            getTypeDataJson={() => JSON.stringify(configValues)}
          />
        </div>
      )}

      {check?.type === "Heartbeat" && (
        <div className="border-t pt-4">
          <HeartbeatPanel serviceSlug={serviceSlug} checkSlug={checkSlug} />
        </div>
      )}

      <div className="flex justify-end">
        <Button type="submit" disabled={updateCheck.isPending || !formState.isDirty}>
          <Save size={14} />
          {saved ? "Saved!" : updateCheck.isPending ? "Saving…" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}

export default ConfigurationSection;
