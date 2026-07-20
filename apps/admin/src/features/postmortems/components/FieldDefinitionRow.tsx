import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { GripVertical, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import type { PostmortemFieldDefinition } from "@/lib/actions/postmortems";

interface Props {
  definition: PostmortemFieldDefinition;
  busy: boolean;
  onToggleActive: (isActive: boolean) => void;
  onDelete: () => void;
}

// One row of the analysis template, drag-reorderable via dnd-kit (grab the handle). System fields
// (the eight seeded sections) can be reordered and toggled active but never deleted; custom fields
// can also be deleted (or deactivated if in use).
function FieldDefinitionRow(props: Props) {
  const { definition, busy, onToggleActive, onDelete } = props;

  const { setNodeRef, transform, transition, isDragging, attributes, listeners } = useSortable({
    id: definition.id,
  });
  const style = { transform: CSS.Transform.toString(transform), transition };

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        "flex items-center gap-3 border-b bg-card px-4 py-3 last:border-b-0",
        isDragging && "relative z-10 shadow-lg"
      )}
    >
      <button
        type="button"
        className="cursor-grab touch-none text-muted-foreground hover:text-foreground active:cursor-grabbing disabled:opacity-50"
        aria-label="Drag to reorder"
        disabled={busy}
        {...attributes}
        {...listeners}
      >
        <GripVertical size={16} />
      </button>

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
          <Switch checked={definition.isActive} onCheckedChange={onToggleActive} disabled={busy} />
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
