import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import type { Tag } from "@/lib/actions/tags";
import { useRequiredWorkerTags, useReplaceRequiredWorkerTags } from "@/hooks/useTags";
import { validateWorkerTagKey } from "@/features/tags/validations";
import KeyValueTagEditor from "@/features/tags/components/KeyValueTagEditor";

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

  if (isLoading || !data) {
    return <p className="text-sm text-muted-foreground">Loading…</p>;
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

      <KeyValueTagEditor
        tags={draft}
        onChange={setDraft}
        validateKey={validateWorkerTagKey}
        systemReadOnly={false}
        placeholder="require a worker tag, e.g. piro:region=eu"
      />

      <div className="flex items-center gap-2">
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
