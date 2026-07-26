import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { tagsApi } from "@/lib/actions/tags";
import { QUERY_KEYS } from "@/constants/api";
import type { components } from "@/lib/api-types";
import TagSelectorTermRow from "./TagSelectorTermRow";

type TagSelector = components["schemas"]["TagSelector"];
type TagTerm = components["schemas"]["TagTerm"];

interface Props {
  /** Current selector, or null for "no tag filter". */
  value: TagSelector | null;
  /** Emits the edited selector, or null when no terms remain (so the subscription matches on severity only). */
  onChange: (next: TagSelector | null) => void;
}

/**
 * Authors the AllOf (ANDed) part of a tag selector (RFC 0008 §4.2) for a subscription filter — the
 * common case, "match when the service has all of these tags". Any AnyOf group already on the selector
 * is preserved untouched and surfaced as a read-only note, but this editor only adds/edits AllOf terms.
 * Empty AllOf + empty AnyOf collapses the whole selector back to null (no filter).
 */
function TagSelectorEditor(props: Props) {
  const { value, onChange } = props;

  const allOf = useMemo(() => value?.allOf ?? [], [value]);
  const anyOfCount = value?.anyOf?.length ?? 0;

  // Include the curated piro:* system keys — filtering notifications on e.g. piro:3rd-party is a
  // primary use case. Distinct query key so it doesn't share the user-only-keys cache used elsewhere.
  const { data: keySuggestions = [] } = useQuery({
    queryKey: [...QUERY_KEYS.TAG_KEYS(""), "withSystem"],
    queryFn: () => tagsApi.keys(undefined, true),
  });

  function emit(nextAllOf: TagTerm[]) {
    const anyOf = value?.anyOf ?? null;
    // Collapse to null when nothing remains, so we store "no filter" rather than an empty selector.
    if (nextAllOf.length === 0 && (anyOf === null || anyOf.length === 0)) {
      onChange(null);
      return;
    }
    onChange({ allOf: nextAllOf.length > 0 ? nextAllOf : null, anyOf });
  }

  function addTerm() {
    emit([...allOf, { key: "", op: "Equals", values: [] }]);
  }

  function updateTerm(index: number, patch: Partial<TagTerm>) {
    const next = allOf.map((t, i) => (i === index ? { ...t, ...patch } : t));
    emit(next);
  }

  function removeTerm(index: number) {
    emit(allOf.filter((_, i) => i !== index));
  }

  return (
    <div className="flex flex-col gap-2">
      {allOf.map((term, index) => (
        <TagSelectorTermRow
          key={index}
          term={term}
          keySuggestions={keySuggestions}
          onChange={(patch) => updateTerm(index, patch)}
          onRemove={() => removeTerm(index)}
        />
      ))}

      <Button type="button" variant="outline" size="sm" className="self-start" onClick={addTerm}>
        <Plus size={14} className="mr-1" /> Add tag condition
      </Button>

      {anyOfCount > 0 && (
        <p className="text-xs text-muted-foreground">
          This filter also has {anyOfCount} “any of” condition{anyOfCount === 1 ? "" : "s"} authored elsewhere; they’re
          preserved but not editable here.
        </p>
      )}
      {allOf.length === 0 && anyOfCount === 0 && (
        <p className="text-xs text-muted-foreground">No tag conditions — the subscription matches on severity alone.</p>
      )}
    </div>
  );
}

export default TagSelectorEditor;
