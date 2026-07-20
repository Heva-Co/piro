import { useNavigate } from "react-router-dom";
import axios from "axios";
import { toast } from "sonner";
import {
  DndContext,
  closestCenter,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy, arrayMove } from "@dnd-kit/sortable";
import { PageHeader } from "@/components/PageHeader";
import { Skeleton } from "@/components/ui/skeleton";
import AddFieldDefinitionForm from "@/features/postmortems/components/AddFieldDefinitionForm";
import FieldDefinitionRow from "@/features/postmortems/components/FieldDefinitionRow";
import {
  usePostmortemFieldDefinitions,
  useCreateFieldDefinition,
  useUpdateFieldDefinition,
  useReorderFieldDefinitions,
  useDeleteFieldDefinition,
} from "@/hooks/usePostmortems";
import { useConfirmDialog } from "@/hooks/useConfirmDialog";
import type { CreateFieldDefinitionRequest } from "@/lib/actions/postmortems";
import { ROUTES } from "@/constants/routes";

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function PostmortemTemplatePage() {
  const navigate = useNavigate();
  const confirm = useConfirmDialog();
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));
  const { data: definitions, isLoading } = usePostmortemFieldDefinitions(true);
  const createField = useCreateFieldDefinition();
  const updateField = useUpdateFieldDefinition();
  const reorderFields = useReorderFieldDefinitions();
  const deleteField = useDeleteFieldDefinition();

  const busy =
    createField.isPending ||
    updateField.isPending ||
    reorderFields.isPending ||
    deleteField.isPending;

  const fields = definitions ?? [];

  async function handleAdd(data: CreateFieldDefinitionRequest) {
    try {
      await createField.mutateAsync(data);
      toast.success("Field added.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to add field."));
    }
  }

  async function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) return;
    const oldIndex = fields.findIndex((f) => f.id === active.id);
    const newIndex = fields.findIndex((f) => f.id === over.id);
    if (oldIndex === -1 || newIndex === -1) return;
    const orderedIds = arrayMove(fields, oldIndex, newIndex).map((f) => f.id);
    try {
      await reorderFields.mutateAsync(orderedIds);
      toast.success("Template reordered.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to reorder fields."));
    }
  }

  async function handleToggleActive(defId: number, isActive: boolean) {
    // The generated UpdateFieldDefinitionRequest treats every field as required-but-nullable; send the
    // untouched ones as null so the backend leaves them unchanged.
    try {
      await updateField.mutateAsync({
        defId,
        data: { heading: null, helpText: null, fieldType: null, sortOrder: null, isActive },
      });
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to update field."));
    }
  }

  async function handleDelete(defId: number, heading: string) {
    const ok = await confirm({
      title: `Delete "${heading}"?`,
      description: "If any postmortem has used this field it will be deactivated instead, preserving its content.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (!ok) return;
    try {
      await deleteField.mutateAsync(defId);
      toast.success("Field removed.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to delete field."));
    }
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Postmortems", onClick: () => navigate(ROUTES.POSTMORTEMS.LIST) },
          { label: "Template" },
        ]}
      />

      <div className="mb-6">
        <h1 className="text-xl font-bold">Analysis template</h1>
        <p className="text-sm text-muted-foreground mt-1">
          The sections every postmortem is structured around. Reorder them, toggle which are active, and
          add your own custom fields. The eight standard sections can't be deleted.
        </p>
      </div>

      <div className="flex flex-col gap-6">
        <div className="rounded-xl border bg-card overflow-hidden">
          {isLoading ? (
            <div className="flex flex-col gap-3 p-4">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : (
            <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
              <SortableContext items={fields.map((f) => f.id)} strategy={verticalListSortingStrategy}>
                {fields.map((f) => (
                  <FieldDefinitionRow
                    key={f.id}
                    definition={f}
                    busy={busy}
                    onToggleActive={(isActive) => handleToggleActive(f.id, isActive)}
                    onDelete={() => handleDelete(f.id, f.heading)}
                  />
                ))}
              </SortableContext>
            </DndContext>
          )}
        </div>

        <AddFieldDefinitionForm saving={createField.isPending} onSubmit={handleAdd} />
      </div>
    </>
  );
}

export default PostmortemTemplatePage;
