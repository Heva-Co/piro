import { useState, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { tagsApi } from "@/lib/actions/tags";
import type { Tag } from "@/lib/actions/tags";
import { QUERY_KEYS } from "@/constants/api";
import { useRequiredWorkerTags, useReplaceRequiredWorkerTags } from "@/hooks/useTags";
import { validateWorkerTagKey, MAX_TAGS_PER_ENTITY } from "@/features/tags/validations";
import TagRow from "@/features/tags/components/TagRow";

interface Props {
  checkId: number;
}

function CheckRequiredWorkerTagsSection(props: Props) {
  const { checkId } = props;
  const { data, isLoading } = useRequiredWorkerTags(checkId);
  const replace = useReplaceRequiredWorkerTags(checkId);

  const [draft, setDraft] = useState<Tag[]>([]);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (data) setDraft(data.tags);
  }, [data]);

  // Keys autocomplete against the known tag vocabulary (workers advertise from the same catalog).
  const { data: keySuggestions = [] } = useQuery({
    queryKey: QUERY_KEYS.TAG_KEYS(""),
    queryFn: () => tagsApi.keys(),
  });

  if (isLoading || !data) {
    return <p className="text-sm text-muted-foreground">Loading…</p>;
  }

  function updateRow(index: number, next: Partial<Tag>) {
    setDraft((rows) => rows.map((t, i) => (i === index ? { ...t, ...next } : t)));
  }

  async function handleSave() {
    setSaved(false);
    const tags = draft.filter((t) => t.key.trim().length > 0);
    await replace.mutateAsync({ tags });
    setSaved(true);
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted-foreground">
        This check runs only on workers carrying a matching tag. Leave empty to run on any worker.
      </p>

      <div className="flex flex-col gap-2">
        {draft.map((t, i) => (
          <TagRow
            key={i}
            keyValue={t.key}
            value={t.value}
            keySuggestions={keySuggestions}
            valueSuggestions={[]}
            validateKey={validateWorkerTagKey}
            onKeyChange={(k) => updateRow(i, { key: k })}
            onValueChange={(v) => updateRow(i, { value: v === "" ? null : v })}
            onRemove={() => setDraft((rows) => rows.filter((_, idx) => idx !== i))}
          />
        ))}
      </div>

      <div className="flex items-center gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setDraft((rows) => [...rows, { key: "", value: null }])}
          disabled={draft.length >= MAX_TAGS_PER_ENTITY}
        >
          Add required tag
        </Button>
        <Button type="button" size="sm" onClick={handleSave} disabled={replace.isPending}>
          {replace.isPending ? "Saving…" : "Save"}
        </Button>
        {saved && !replace.isPending && <span className="text-xs text-green-600">Saved</span>}
        {replace.isError && <span className="text-xs text-destructive">Save failed.</span>}
      </div>
    </div>
  );
}

export default CheckRequiredWorkerTagsSection;
