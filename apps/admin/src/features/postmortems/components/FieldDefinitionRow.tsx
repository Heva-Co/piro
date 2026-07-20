import { ArrowDown, ArrowUp, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import type { PostmortemFieldDefinition } from "@/lib/actions/postmortems";

interface Props {
  definition: PostmortemFieldDefinition;
  isFirst: boolean;
  isLast: boolean;
  busy: boolean;
  onMove: (direction: "up" | "down") => void;
  onToggleActive: (isActive: boolean) => void;
  onDelete: () => void;
}

// One row of the analysis template. System fields (the eight seeded sections) can be reordered and
// toggled active but never deleted; custom fields can also be deleted (or deactivated if in use).
function FieldDefinitionRow(props: Props) {
  const { definition, isFirst, isLast, busy, onMove, onToggleActive, onDelete } = props;

  return (
    <div className="flex items-center gap-3 border-b px-4 py-3 last:border-b-0">
      <div className="flex flex-col">
        <Button
          variant="ghost"
          size="icon-xs"
          onClick={() => onMove("up")}
          disabled={busy || isFirst}
          aria-label="Move up"
        >
          <ArrowUp size={12} />
        </Button>
        <Button
          variant="ghost"
          size="icon-xs"
          onClick={() => onMove("down")}
          disabled={busy || isLast}
          aria-label="Move down"
        >
          <ArrowDown size={12} />
        </Button>
      </div>

      <div className="flex flex-1 flex-col">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{definition.heading}</span>
          {definition.isSystem ? (
            <Badge variant="secondary">System</Badge>
          ) : (
            <Badge variant="outline">Custom</Badge>
          )}
          <Badge variant="ghost">{definition.fieldType}</Badge>
        </div>
        <code className="text-xs font-mono text-muted-foreground">{definition.key}</code>
        {definition.helpText && (
          <span className="text-xs text-muted-foreground">{definition.helpText}</span>
        )}
      </div>

      <div className="flex items-center gap-4">
        <label className="flex items-center gap-2 text-xs text-muted-foreground">
          Active
          <Switch
            checked={definition.isActive}
            onCheckedChange={onToggleActive}
            disabled={busy}
          />
        </label>
        {!definition.isSystem && (
          <Button
            variant="ghost"
            size="icon-sm"
            onClick={onDelete}
            disabled={busy}
            aria-label="Delete field"
          >
            <Trash2 size={13} />
          </Button>
        )}
      </div>
    </div>
  );
}

export default FieldDefinitionRow;
