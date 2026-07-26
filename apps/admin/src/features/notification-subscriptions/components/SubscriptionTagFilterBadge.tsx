import { Tag } from "lucide-react";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { components } from "@/lib/api-types";

type TagSelector = components["schemas"]["TagSelector"];
type TagTerm = components["schemas"]["TagTerm"];

interface Props {
  filter: TagSelector | null | undefined;
}

const OP_LABELS: Record<TagTerm["op"], string> = {
  Equals: "=",
  In: "in",
  NotIn: "not in",
  Exists: "exists",
};

function describeTerm(term: TagTerm): string {
  const values = term.values ?? [];
  if (term.op === "Exists") return term.key;
  if (term.op === "Equals") return `${term.key} = ${values[0] ?? ""}`;
  return `${term.key} ${OP_LABELS[term.op]} [${values.join(", ")}]`;
}

/**
 * A tag icon shown on a subscription row when it carries a tag filter (RFC 0008 §4.2). Nothing renders
 * when there's no filter. Hovering reveals a styled tooltip listing each term — AllOf terms are ANDed,
 * AnyOf terms are ORed — so the filter is legible at a glance without opening the edit modal.
 */
function SubscriptionTagFilterBadge(props: Props) {
  const { filter } = props;

  const allOf = filter?.allOf ?? [];
  const anyOf = filter?.anyOf ?? [];
  if (allOf.length === 0 && anyOf.length === 0) return null;

  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <span className="inline-flex" aria-label="Has a tag filter">
            <Tag size={13} className="shrink-0 text-muted-foreground" />
          </span>
        }
      />
      <TooltipContent className="max-w-xs">
        <div className="flex flex-col gap-1.5">
          <span className="font-medium">Tag filter</span>
          {allOf.length > 0 && (
            <div className="flex flex-col gap-0.5">
              <span className="text-[10px] uppercase tracking-wide opacity-70">Must match all</span>
              {allOf.map((term, i) => (
                <span key={`all-${i}`} className="font-mono text-xs">{describeTerm(term)}</span>
              ))}
            </div>
          )}
          {anyOf.length > 0 && (
            <div className="flex flex-col gap-0.5">
              <span className="text-[10px] uppercase tracking-wide opacity-70">Must match any</span>
              {anyOf.map((term, i) => (
                <span key={`any-${i}`} className="font-mono text-xs">{describeTerm(term)}</span>
              ))}
            </div>
          )}
        </div>
      </TooltipContent>
    </Tooltip>
  );
}

export default SubscriptionTagFilterBadge;
