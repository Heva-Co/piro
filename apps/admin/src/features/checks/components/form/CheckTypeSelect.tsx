import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { CheckTypeMeta } from "@/lib/actions/checks";
import type { components } from "@/lib/api-types";

type CheckType = components["schemas"]["CheckType"];

interface Props {
  value: CheckType;
  checkTypes: CheckTypeMeta[];
  /** Integration types that are actually connected — gates types that require one. */
  connectedIntegrationTypes: Set<string>;
  onChange: (type: CheckType) => void;
}

function CheckTypeSelect(props: Props) {
  const { value, checkTypes, connectedIntegrationTypes, onChange } = props;

  return (
    <Select value={value} onValueChange={(v) => v && onChange(v as CheckType)}>
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
}

export default CheckTypeSelect;
