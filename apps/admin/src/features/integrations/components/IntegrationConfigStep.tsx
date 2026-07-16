import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/components/PageHeader";
import { integrationsApi, integrationTypesApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { IntegrationConfigForm } from "./IntegrationConfigForm";

interface Props {
  /** Integration id in edit mode; undefined when creating a new one. */
  id?: string;
  /** Preselected type — required when creating (comes from the type-picker step), unused in edit mode. */
  initialType: string;
  /** Called when the user backs out of type selection (create mode only). */
  onBack?: () => void;
}

/**
 * Loads the existing Integration (edit mode) and every type's manifest before rendering
 * IntegrationConfigForm, so the form's initial state can be derived directly from loaded data
 * instead of synchronized into it via a post-mount effect.
 */
export function IntegrationConfigStep(props: Props) {
  const { id, initialType, onBack } = props;
  const isEdit = Boolean(id);

  const { data: existing } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION(id!),
    queryFn: () => integrationsApi.get(id!),
    enabled: isEdit,
  });
  const { data: allTypes } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_TYPES,
    queryFn: integrationTypesApi.list,
  });

  if (!allTypes || (isEdit && !existing))
    return <PageHeader breadcrumbs={[{ label: "Integrations" }, { label: "Loading…" }]} />;

  const resolvedType = isEdit ? existing!.type : initialType;
  const typeMeta = allTypes.find((t) => t.type === resolvedType);

  return (
    <IntegrationConfigForm
      id={id}
      existing={existing}
      resolvedType={resolvedType}
      typeMeta={typeMeta}
      onBack={onBack}
    />
  );
}
