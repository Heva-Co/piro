import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { X } from "lucide-react";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { tagsApi } from "@/lib/actions/tags";
import { QUERY_KEYS } from "@/constants/api";
import type { components } from "@/lib/api-types";
import SuggestionList from "./SuggestionList";

type TagTerm = components["schemas"]["TagTerm"];
type TagOp = components["schemas"]["TagOp"];

// Operators that need at least one value; Exists is key-only.
const VALUED_OPS: TagOp[] = ["Equals", "In", "NotIn"];
// Operators that take exactly one value — the value field autocompletes; multi-value ops don't (the
// user is typing a comma-separated list, so a single-suggestion picker would fight the input).
const SINGLE_VALUE_OPS: TagOp[] = ["Equals"];

const OP_LABELS: Record<TagOp, string> = {
  Equals: "equals",
  In: "in",
  NotIn: "not in",
  Exists: "exists",
};

interface Props {
  term: TagTerm;
  keySuggestions: string[];
  onChange: (patch: Partial<TagTerm>) => void;
  onRemove: () => void;
}

/** One editable tag-selector term: key (with autocomplete), operator, and — for valued operators — values. */
function TagSelectorTermRow(props: Props) {
  const { term, keySuggestions, onChange, onRemove } = props;
  const [keyFocused, setKeyFocused] = useState(false);
  const [valueFocused, setValueFocused] = useState(false);

  const needsValues = VALUED_OPS.includes(term.op);
  const singleValue = SINGLE_VALUE_OPS.includes(term.op);

  const keyMatches = keySuggestions
    .filter((k) => k.toLowerCase().startsWith(term.key.trim().toLowerCase()) && k !== term.key.trim())
    .slice(0, 8);
  const showKeyDropdown = keyFocused && keyMatches.length > 0;

  // Value suggestions load once a real key is chosen; only used for single-value operators.
  const trimmedKey = term.key.trim();
  const { data: valueSuggestions = [] } = useQuery({
    queryKey: QUERY_KEYS.TAG_VALUES(trimmedKey),
    queryFn: () => tagsApi.values(trimmedKey),
    enabled: singleValue && trimmedKey.length > 0,
  });
  const currentValue = term.values?.[0] ?? "";
  const valueMatches = valueSuggestions
    .filter((v) => v.toLowerCase().startsWith(currentValue.toLowerCase()) && v !== currentValue)
    .slice(0, 8);
  const showValueDropdown = valueFocused && singleValue && valueMatches.length > 0;

  function setOp(op: TagOp) {
    // Dropping to a key-only operator clears any values so we never persist stale ones.
    onChange(VALUED_OPS.includes(op) ? { op } : { op, values: [] });
  }

  return (
    <div className="flex items-start gap-2">
      <div className="relative flex-1">
        <Input
          value={term.key}
          onChange={(e) => onChange({ key: e.target.value })}
          onFocus={() => setKeyFocused(true)}
          onBlur={() => setTimeout(() => setKeyFocused(false), 150)}
          placeholder="key, e.g. env"
          className="font-mono"
        />
        {showKeyDropdown && (
          <SuggestionList
            items={keyMatches}
            onPick={(k) => { onChange({ key: k }); setKeyFocused(false); }}
          />
        )}
      </div>

      <Select value={term.op} onValueChange={(v) => setOp(v as TagOp)}>
        <SelectTrigger className="w-28 shrink-0">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {(Object.keys(OP_LABELS) as TagOp[]).map((op) => (
            <SelectItem key={op} value={op}>{OP_LABELS[op]}</SelectItem>
          ))}
        </SelectContent>
      </Select>

      {needsValues && (
        <div className="relative flex-1">
          <Input
            value={(term.values ?? []).join(", ")}
            onChange={(e) =>
              onChange({ values: e.target.value.split(",").map((s) => s.trim()).filter((s) => s.length > 0) })
            }
            onFocus={() => setValueFocused(true)}
            onBlur={() => setTimeout(() => setValueFocused(false), 150)}
            placeholder={singleValue ? "value" : "value1, value2"}
            className="font-mono"
          />
          {showValueDropdown && (
            <SuggestionList
              items={valueMatches}
              onPick={(v) => { onChange({ values: [v] }); setValueFocused(false); }}
            />
          )}
        </div>
      )}

      <Button type="button" variant="ghost" size="icon" className="shrink-0" onClick={onRemove} aria-label="Remove condition">
        <X size={14} />
      </Button>
    </div>
  );
}

export default TagSelectorTermRow;
