import { useQuery } from "@tanstack/react-query";
import { TriangleAlert } from "lucide-react";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tooltip, TooltipTrigger, TooltipContent } from "@/components/ui/tooltip";
import { escalationApi } from "@/lib/actions/escalation";
import { QUERY_KEYS } from "@/constants/api";

const NO_POLICY = "__none__";

interface Props {
  value: number | null;
  onChange: (value: number | null) => void;
}

/** On-call escalation policy picker for an Integration whose alerts have no Service to inherit one from — see IntegrationCapability.SupportsEscalationPolicy. */
export function IntegrationEscalationPolicyField(props: Props) {
  const { value, onChange } = props;

  const { data: policiesPage } = useQuery({
    queryKey: QUERY_KEYS.ESCALATION_POLICIES,
    queryFn: () => escalationApi.list({ pageSize: 200 }),
  });
  const policies = policiesPage?.items ?? [];

  const selectValue = value != null ? String(value) : NO_POLICY;
  const selectedName = policies.find((p) => p.id === value)?.name;

  return (
    <div className="flex flex-col gap-1.5">
      <Label className="flex items-center gap-1.5">
        Escalation Policy
        {value == null && (
          <Tooltip>
            <TooltipTrigger render={<span className="inline-flex" />}>
              <TriangleAlert size={14} className="text-amber-500" />
            </TooltipTrigger>
            <TooltipContent>
              No escalation policy assigned — if this integration produces an alert with no Service
              to inherit one from, on-call won't be notified.
            </TooltipContent>
          </Tooltip>
        )}
      </Label>
      <Select
        value={selectValue}
        onValueChange={(v) => onChange(v === NO_POLICY ? null : Number(v))}
      >
        <SelectTrigger className="w-full">
          <SelectValue placeholder="None">
            {value == null ? "None" : selectedName ?? "None"}
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={NO_POLICY}>None</SelectItem>
          {policies.map((p) => (
            <SelectItem key={p.id} value={String(p.id)}>{p.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">
        On-call escalation used for alerts from this integration with no Service of their own.
      </p>
    </div>
  );
}
