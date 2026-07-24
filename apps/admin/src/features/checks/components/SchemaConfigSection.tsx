import { useController, useFormContext } from "react-hook-form";
import DynamicConfigForm from "@/components/config-form/DynamicConfigForm";
import RequiredIntegrationPicker from "@/features/checks/components/RequiredIntegrationPicker";
import type { CheckTypeMeta } from "@/lib/actions/checks";

interface Props {
  /** The selected type's manifest, whose configSchema drives the rendered fields (RFC 0011). */
  typeMeta?: CheckTypeMeta;
  /** Per-field validation errors (from validateConfig on submit), keyed by field key. */
  errors?: Record<string, string>;
  /** Error for the required-integration picker, when the type needs one and none is chosen. */
  integrationError?: string;
}

/**
 * Bridges the schema-driven DynamicConfigForm to react-hook-form: config values live under the
 * form's `config` field (a structured object), and this reads/writes them via useController so the
 * rest of CheckFormPage stays on RHF. Rendering, composite controls, and conditional visibility are
 * all handled by DynamicConfigForm from `typeMeta.configSchema`.
 */
/**
 * ConfigJson key holding the required integration instance id. A check that requires a provider
 * integration reads this from its own config (e.g. GcpCloudRunJobCheckConfig.IntegrationInstanceId,
 * camelCased). The RequiredIntegrationPicker writes it and the raw field is hidden from the schema form,
 * so there's one control instead of a picker plus a raw GUID input.
 */
const INTEGRATION_INSTANCE_KEY = "integrationInstanceId";

function SchemaConfigSection(props: Props) {
  const { typeMeta, errors, integrationError } = props;
  const { control } = useFormContext();
  const { field } = useController({ name: "config", control });

  const values = (field.value ?? {}) as Record<string, unknown>;
  const schema = typeMeta?.configSchema ?? [];
  const requiredIntegration = typeMeta?.requiredIntegrationType;

  if (schema.length === 0 && !requiredIntegration)
    return <p className="text-sm text-muted-foreground">This check type has no configuration.</p>;

  // The picker owns the integration-instance field, so drop it from the generically-rendered schema.
  const visibleSchema = requiredIntegration
    ? schema.filter((f) => f.key !== INTEGRATION_INSTANCE_KEY)
    : schema;

  return (
    <div className="flex flex-col gap-4">
      {requiredIntegration && (
        <RequiredIntegrationPicker
          integrationType={requiredIntegration}
          value={(values[INTEGRATION_INSTANCE_KEY] as string) ?? ""}
          onChange={(id) => field.onChange({ ...values, [INTEGRATION_INSTANCE_KEY]: id })}
          error={integrationError}
        />
      )}
      {visibleSchema.length > 0 && (
        <DynamicConfigForm schema={visibleSchema} values={values} errors={errors} onChange={field.onChange} />
      )}
    </div>
  );
}

export default SchemaConfigSection;
