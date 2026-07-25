import { Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { DraftOverride } from "../hooks/useRotationsDraft";

interface Props {
  overrides: DraftOverride[];
  onDelete: (overrideId: number) => void;
}

function StagedOverridesList(props: Props) {
  const { overrides, onDelete } = props;

  return (
    <div className="rounded-lg border divide-y">
      {overrides.length === 0 ? (
        <p className="text-sm text-muted-foreground italic px-3 py-2">No overrides staged.</p>
      ) : (
        overrides.map((ov) => (
          <div key={ov.id} className="flex items-center justify-between gap-3 px-3 py-2">
            <div className="flex items-center gap-2 min-w-0 text-sm">
              <span className="font-medium truncate">
                {ov.replacesUserName ? `${ov.userName} → replacing ${ov.replacesUserName}` : `${ov.userName} (extra coverage)`}
              </span>
              {ov.isNew && (
                <Badge className="bg-blue-500/15 text-blue-600 dark:text-blue-400">New</Badge>
              )}
            </div>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => onDelete(ov.id)}
              title="Delete override"
              className="text-muted-foreground hover:text-destructive shrink-0"
            >
              <Trash2 size={13} />
            </Button>
          </div>
        ))
      )}
    </div>
  );
}

export default StagedOverridesList;
