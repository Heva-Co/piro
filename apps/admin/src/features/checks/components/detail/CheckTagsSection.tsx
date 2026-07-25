import { useState, useEffect } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { FieldSeparator } from "@/components/ui/field";
import { useCheckTags, useReplaceCheckTags, useToggleCheckSystemTag } from "@/hooks/useTags";
import type { Tag } from "@/lib/actions/tags";
import { isSystemKey } from "@/features/tags/validations";
import KeyValueTagEditor from "@/features/tags/components/KeyValueTagEditor";
import AssignableSystemTags from "@/features/tags/components/AssignableSystemTags";

interface Props {
  checkId: number;
}

function CheckTagsSection(props: Props) {
  const { checkId } = props;
  const { data, isLoading } = useCheckTags(checkId);
  const replace = useReplaceCheckTags(checkId);
  const toggleSystem = useToggleCheckSystemTag(checkId);

  // Editable draft of the check's OWN user tags; system rows are shown but never edited here.
  const [draft, setDraft] = useState<Tag[]>([]);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (data) setDraft(data.own);
  }, [data]);

  if (isLoading || !data) {
    return <p className="text-sm text-muted-foreground">Loading tags…</p>;
  }

  // Inherited = effective minus own (by key). Shown read-only, labelled as coming from the service.
  const ownKeys = new Set(draft.map((t) => t.key));
  const inherited = data.effective.filter((t) => !data.own.some((o) => o.key === t.key));

  const systemOwn = data.own.filter((t) => isSystemKey(t.key));
  const userOwn = draft.filter((t) => !isSystemKey(t.key));

  async function handleSave() {
    setSaved(false);
    // Only user rows are submitted (replace semantics); drop blank keys.
    const userTags = userOwn.filter((t) => t.key.trim().length > 0);
    await replace.mutateAsync({ tags: userTags });
    setSaved(true);
  }

  return (
    <div className="flex flex-col gap-4">
      <KeyValueTagEditor tags={[...systemOwn, ...userOwn]} onChange={setDraft} />

      {inherited.length > 0 && (
        <div className="flex flex-col gap-1.5">
          <p className="text-xs font-semibold text-muted-foreground">Inherited from service</p>
          <div className="flex flex-wrap gap-1.5">
            {inherited.map((t) => (
              <Badge
                key={t.key}
                variant="outline"
                className={`font-mono ${ownKeys.has(t.key) ? "line-through opacity-60" : ""}`}
                title={ownKeys.has(t.key) ? "Overridden by this check" : "Inherited from the parent service"}
              >
                {t.value != null ? `${t.key}=${t.value}` : t.key}
              </Badge>
            ))}
          </div>
        </div>
      )}

      <div className="flex items-center gap-2">
        <Button type="button" size="sm" onClick={handleSave} disabled={replace.isPending}>
          {replace.isPending ? "Saving…" : "Save tags"}
        </Button>
        {saved && !replace.isPending && <span className="text-xs text-green-600">Saved</span>}
        {replace.isError && <span className="text-xs text-destructive">Save failed. Check the tag rules.</span>}
      </div>

      <FieldSeparator />

      <div className="flex flex-col gap-2">
        <p className="text-xs font-semibold text-muted-foreground">System flags</p>
        <AssignableSystemTags
          tags={data.own}
          disabled={toggleSystem.isPending}
          onToggle={(key, assigned) => toggleSystem.mutate({ key, assigned })}
        />
      </div>
    </div>
  );
}

export default CheckTagsSection;
