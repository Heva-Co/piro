import { useState } from "react";
import { Eye, EyeOff } from "lucide-react";
import { Button } from "@/components/ui/button";
import { WarningConfirmDialog } from "@/components/ui/warning-confirm-dialog";

interface Props {
  isPublic: boolean;
  isMerged: boolean;
  isPublishing: boolean;
  isUnpublishing: boolean;
  onPublish: () => void;
  onUnpublish: () => void;
}

function IncidentVisibilitySection(props: Props) {
  const { isPublic, isMerged, isPublishing, isUnpublishing, onPublish, onUnpublish } = props;
  const [confirmUnpublishOpen, setConfirmUnpublishOpen] = useState(false);

  if (!isPublic) {
    return (
      <div className="rounded-xl border border-yellow-500/30 bg-yellow-500/10 p-5 flex items-center justify-between gap-3">
        <p className="text-xs text-yellow-700 dark:text-yellow-500">
          This incident is private. Publish it to make it (and any Public updates) visible on the status page.
        </p>
        {!isMerged && (
          <Button onClick={onPublish} disabled={isPublishing}>
            <Eye size={12} /> {isPublishing ? "Publishing…" : "Publish Now"}
          </Button>
        )}
      </div>
    );
  }

  return (
    <>
      <div className="rounded-xl border border-green-500/30 bg-green-500/10 p-5 flex items-center justify-between gap-3">
        <p className="text-xs text-green-700 dark:text-green-500">
          This incident and its public updates are visible on the status page.
        </p>
        {!isMerged && (
          <Button variant="outline" onClick={() => setConfirmUnpublishOpen(true)} disabled={isUnpublishing}>
            <EyeOff size={12} /> {isUnpublishing ? "Unpublishing…" : "Unpublish"}
          </Button>
        )}
      </div>

      <WarningConfirmDialog
        open={confirmUnpublishOpen}
        onOpenChange={setConfirmUnpublishOpen}
        title="Unpublish this incident?"
        description="It will be hidden from the status page and all its public updates will become internal."
        confirmLabel="Unpublish"
        confirmPendingLabel="Unpublishing…"
        isPending={isUnpublishing}
        onConfirm={() => { onUnpublish(); setConfirmUnpublishOpen(false); }}
      />
    </>
  );
}

export default IncidentVisibilitySection;
