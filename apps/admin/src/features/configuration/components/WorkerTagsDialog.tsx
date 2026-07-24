import { useState, useEffect } from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { useWorkerTags, useReplaceWorkerTags } from "@/hooks/useTags";
import type { Tag } from "@/lib/actions/tags";
import { isSystemKey } from "@/features/tags/validations";
import KeyValueTagEditor from "@/features/tags/components/KeyValueTagEditor";

interface Props {
  /** The worker being edited, or null when the dialog is closed. */
  worker: { id: string; name: string } | null;
  onClose: () => void;
}

function WorkerTagsDialog(props: Props) {
  const { worker, onClose } = props;
  const { data, isLoading } = useWorkerTags(worker?.id);
  const replace = useReplaceWorkerTags(worker?.id ?? "");

  const [draft, setDraft] = useState<Tag[]>([]);

  useEffect(() => {
    if (data) setDraft(data.tags);
  }, [data]);

  const systemTags = (data?.tags ?? []).filter((t) => isSystemKey(t.key));
  const userDraft = draft.filter((t) => !isSystemKey(t.key));

  async function handleSave() {
    const tags = userDraft.filter((t) => t.key.trim().length > 0);
    await replace.mutateAsync({ tags });
    onClose();
  }

  return (
    <Dialog open={worker != null} onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Worker tags</DialogTitle>
          <DialogDescription>
            {worker ? `Tags advertised by "${worker.name}". Checks require these to run here.` : ""}
          </DialogDescription>
        </DialogHeader>

        {isLoading || !data ? (
          <p className="text-sm text-muted-foreground">Loading tags…</p>
        ) : (
          <KeyValueTagEditor tags={[...systemTags, ...userDraft]} onChange={setDraft} />
        )}

        {replace.isError && <p className="text-xs text-destructive">Save failed. Check the tag rules.</p>}

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="button" onClick={handleSave} disabled={replace.isPending || isLoading}>
            {replace.isPending ? "Saving…" : "Save"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default WorkerTagsDialog;
