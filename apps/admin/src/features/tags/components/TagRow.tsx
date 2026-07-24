import { useId } from "react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { validateUserKey, validateValue } from "@/features/tags/validations";

interface Props {
  keyValue: string;
  value: string | null;
  /** Known keys for key autocomplete. */
  keySuggestions: string[];
  /** Known values for the current key, for value autocomplete. */
  valueSuggestions: string[];
  /** Key validator; defaults to the user-tag rules. Required-worker-tags pass a looser one that allows piro:*. */
  validateKey?: (key: string) => string | null;
  onKeyChange: (key: string) => void;
  onValueChange: (value: string) => void;
  onRemove: () => void;
}

function TagRow(props: Props) {
  const {
    keyValue, value, keySuggestions, valueSuggestions,
    validateKey = validateUserKey, onKeyChange, onValueChange, onRemove,
  } = props;
  const keyListId = useId();
  const valueListId = useId();

  const keyError = keyValue.length > 0 ? validateKey(keyValue) : null;
  const valueError = validateValue(value);

  return (
    <div className="flex items-start gap-2">
      <div className="flex-1">
        <Input
          list={keyListId}
          value={keyValue}
          onChange={(e) => onKeyChange(e.target.value)}
          placeholder="key (e.g. team)"
          className="font-mono"
          aria-invalid={keyError != null}
        />
        <datalist id={keyListId}>
          {keySuggestions.map((k) => (
            <option key={k} value={k} />
          ))}
        </datalist>
        {keyError && <p className="mt-1 text-xs text-destructive">{keyError}</p>}
      </div>
      <div className="flex-1">
        <Input
          list={valueListId}
          value={value ?? ""}
          onChange={(e) => onValueChange(e.target.value)}
          placeholder="value (optional)"
          className="font-mono"
          aria-invalid={valueError != null}
        />
        <datalist id={valueListId}>
          {valueSuggestions.map((v) => (
            <option key={v} value={v} />
          ))}
        </datalist>
        {valueError && <p className="mt-1 text-xs text-destructive">{valueError}</p>}
      </div>
      <Button type="button" variant="ghost" size="sm" onClick={onRemove} aria-label="Remove tag">
        Remove
      </Button>
    </div>
  );
}

export default TagRow;
