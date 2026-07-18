import { useQuery } from "@tanstack/react-query";
import { integrationsApi, integrationTypesApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { PagerDutyServiceMapping } from "./PagerDutyServiceMapping";

interface Props {
  serviceId: number;
}

/**
 * Section listing where this service routes its alerts to third-party systems (RFC 0004). Renders one
 * mapping control per configured integration whose type declares RequiresOAuthConnection — so the
 * section is manifest-driven (it appears only when such an integration exists), even though the
 * per-integration control is PagerDuty-specific for now. Returns null when there's nothing to route to.
 */
export function AlertRoutingSection(props: Props) {
  const { serviceId } = props;

  const { data: integrations } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });
  const { data: types } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_TYPES,
    queryFn: integrationTypesApi.list,
  });

  if (!integrations || !types) return null;

  const oauthTypes = new Set(
    types.filter((t) => t.capabilities.includes("RequiresOAuthConnection")).map((t) => t.type),
  );
  const routable = integrations.filter((i) => oauthTypes.has(i.type));

  if (routable.length === 0)
    return (
      <p className="text-sm text-muted-foreground">
        No routable integrations configured. Create and connect a PagerDuty integration to route this
        service's alerts to it.
      </p>
    );

  return (
    <div className="flex flex-col gap-6">
      {routable.map((integration) => (
        <PagerDutyServiceMapping key={integration.id} serviceId={serviceId} integration={integration} />
      ))}
    </div>
  );
}
