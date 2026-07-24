import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { tagsApi } from "@/lib/actions/tags";
import type { Tag } from "@/lib/actions/tags";
import { QUERY_KEYS } from "@/constants/api";
import { MAX_TAGS_PER_ENTITY, isSystemKey } from "@/features/tags/validations";
import TagRow from "@/features/tags/components/TagRow";

interface Props {
  /** The full tag set (user + reconciled system). System tags render read-only; user tags are editable. */
  tags: Tag[];
  /** Called with the edited USER-tag set only (system rows are never emitted). */
  onChange: (userTags: Tag[]) => void;
}

function KeyValueTagEditor(props: Props) {
  const { tags, onChange } = props;

  const systemTags = tags.filter((t) => isSystemKey(t.key));
  const userTags = tags.filter((t) => !isSystemKey(t.key));

  const { data: keySuggestions = [] } = useQuery({
    queryKey: QUERY_KEYS.TAG_KEYS(""),
    queryFn: () => tagsApi.keys(),
  });

  function updateRow(index: number, next: Partial<Tag>) {
    const copy = userTags.map((t, i) => (i === index ? { ...t, ...next } : t));
    onChange(copy);
  }

  function removeRow(index: number) {
    onChange(userTags.filter((_, i) => i !== index));
  }

  function addRow() {
    if (userTags.length >= MAX_TAGS_PER_ENTITY) return;
    onChange([...userTags, { key: "", value: null }]);
  }

  return (
    <div className="flex flex-col gap-3">
      {systemTags.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {systemTags.map((t) => (
            <Badge key={t.key} variant="secondary" className="font-mono" title="System tag (read-only)">
              {t.value != null ? `${t.key}=${t.value}` : t.key}
            </Badge>
          ))}
        </div>
      )}

      <div className="flex flex-col gap-2">
        {userTags.map((t, i) => (
          <TagRow
            key={i}
            keyValue={t.key}
            value={t.value}
            keySuggestions={keySuggestions}
            valueSuggestions={[]}
            onKeyChange={(k) => updateRow(i, { key: k })}
            onValueChange={(v) => updateRow(i, { value: v === "" ? null : v })}
            onRemove={() => removeRow(i)}
          />
        ))}
      </div>

      <div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={addRow}
          disabled={userTags.length >= MAX_TAGS_PER_ENTITY}
        >
          Add tag
        </Button>
        {userTags.length >= MAX_TAGS_PER_ENTITY && (
          <span className="ml-2 text-xs text-muted-foreground">
            Maximum {MAX_TAGS_PER_ENTITY} tags reached.
          </span>
        )}
      </div>
    </div>
  );
}

export default KeyValueTagEditor;
