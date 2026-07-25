import { useQuery } from "@tanstack/react-query";
import { integrationsApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

interface Props {
  /** The IntegrationType the check requires (from CheckTypeMeta.requiredIntegrationType, RFC 0011). */
  integrationType: string;
  /** The currently selected Integration id, or "" when none chosen. */
  value: string;
  onChange: (id: string) => void;
  error?: string;
}

/**
 * Picks which provider Integration a check uses. Stored in the check's config as the integration
 * instance id (config.integrationInstanceId) — what the check reads to authenticate — and rendered
 * alongside DynamicConfigForm only when the type's manifest declares a required integration (e.g. GCP
 * Cloud Run Job → GoogleCloud). Needs the live list of matching integrations to resolve id → name.
 */
function RequiredIntegrationPicker(props: Props) {
  const { integrationType, value, onChange, error } = props;
  const { data: integrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });

  // The config schema seeds a Guid field with Guid.Empty ("000…0"); treat that as "nothing selected"
  // so the placeholder shows and the id never leaks into the trigger.
  const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";
  const selectedId = value === EMPTY_GUID ? "" : value;

  const matching = integrations.filter((i) => i.type === integrationType);
  const selectedName = matching.find((i) => i.id === selectedId)?.name;

  return (
    <div className="flex flex-col gap-1.5">
      <Label>
        {integrationType} integration <span className="text-destructive">*</span>
      </Label>
      <Select value={selectedId} onValueChange={(v) => v && onChange(v)}>
        <SelectTrigger className="w-full">
          {/* SelectValue with no children shows the raw value (the id); render the integration's name
              instead, and never the id when it can't be resolved. */}
          <SelectValue placeholder={`Select a ${integrationType} integration…`}>
            {selectedId ? selectedName : undefined}
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          {matching.map((i) => (
            <SelectItem key={i.id} value={i.id}>{i.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      {!error && matching.length === 0 && (
        <p className="text-xs text-amber-600 dark:text-amber-400">
          No {integrationType} integration exists yet — create one first.
        </p>
      )}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}

export default RequiredIntegrationPicker;
