import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { FieldSeparator } from "@/components/ui/field";
import { useServiceTags, useReplaceServiceTags, useToggleServiceSystemTag } from "@/hooks/useTags";
import type { Tag } from "@/lib/actions/tags";
import { isSystemKey } from "@/features/tags/validations";
import KeyValueTagEditor from "@/features/tags/components/KeyValueTagEditor";
import AssignableSystemTags from "@/features/tags/components/AssignableSystemTags";

interface Props {
  serviceId: number;
}

function ServiceTagsSection(props: Props) {
  const { serviceId } = props;
  const { data, isLoading } = useServiceTags(serviceId);
  const replace = useReplaceServiceTags(serviceId);
  const toggleSystem = useToggleServiceSystemTag(serviceId);

  const [draft, setDraft] = useState<Tag[]>([]);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (data) setDraft(data.tags);
  }, [data]);

  if (isLoading || !data) {
    return <p className="text-sm text-muted-foreground">Loading tags…</p>;
  }

  const systemTags = data.tags.filter((t) => isSystemKey(t.key));
  const userDraft = draft.filter((t) => !isSystemKey(t.key));

  async function handleSave() {
    setSaved(false);
    const tags = userDraft.filter((t) => t.key.trim().length > 0);
    await replace.mutateAsync({ tags });
    setSaved(true);
  }

  return (
    <div className="flex flex-col gap-4">
      <KeyValueTagEditor tags={[...systemTags, ...userDraft]} onChange={setDraft} />

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
          tags={data.tags}
          disabled={toggleSystem.isPending}
          onToggle={(key, assigned) => toggleSystem.mutate({ key, assigned })}
        />
      </div>
    </div>
  );
}

export default ServiceTagsSection;
