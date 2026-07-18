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
 * Picks which provider Integration a check uses. This is a check-level concern, not part of the
 * schema-driven config form: `integrationId` is a column on the Check (not inside TypeDataJson), and
 * it needs the live list of matching integrations. It is rendered alongside DynamicConfigForm only
 * when the type's manifest declares a required integration (e.g. GCP Cloud Run Job → GoogleCloud).
 */
function RequiredIntegrationPicker(props: Props) {
  const { integrationType, value, onChange, error } = props;
  const { data: integrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });

  const matching = integrations.filter((i) => i.type === integrationType);

  return (
    <div className="flex flex-col gap-1.5">
      <Label>
        {integrationType} integration <span className="text-destructive">*</span>
      </Label>
      <Select value={value} onValueChange={(v) => v && onChange(v)}>
        <SelectTrigger className="w-full">
          <SelectValue placeholder={`Select a ${integrationType} integration…`} />
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
