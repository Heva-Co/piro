import { useParams, useNavigate } from "react-router-dom";
import { Trash2, Send, Undo2 } from "lucide-react";
import axios from "axios";
import { toast } from "react-toastify";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import PostmortemStatusBadge from "@/features/postmortems/components/PostmortemStatusBadge";
import PostmortemFieldsSection from "@/features/postmortems/components/PostmortemFieldsSection";
import PostmortemIncidentsSection from "@/features/postmortems/components/PostmortemIncidentsSection";
import PostmortemTimelineSection from "@/features/postmortems/components/PostmortemTimelineSection";
import PostmortemDetailSkeleton from "@/features/postmortems/components/PostmortemDetailSkeleton";
import {
  usePostmortem,
  useUpdatePostmortem,
  usePublishPostmortem,
  useDeletePostmortem,
  useLinkIncident,
  useUnlinkIncident,
  useAddTimelineEntry,
  useUpdateTimelineEntry,
  useDeleteTimelineEntry,
} from "@/hooks/usePostmortems";
import { useConfirmDialog } from "@/hooks/useConfirmDialog";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import type { PostmortemFieldValueUpdate } from "@/lib/actions/postmortems";
import { ROUTES } from "@/constants/routes";

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function PostmortemDetailPage() {
  const { id } = useParams<{ id: string }>();
  const postmortemId = Number(id);
  const navigate = useNavigate();
  const confirm = useConfirmDialog();
  const { formatDate } = useFormattedDate();

  const { data: postmortem, isLoading, isError } = usePostmortem(postmortemId);
  const updatePostmortem = useUpdatePostmortem(postmortemId);
  const publishPostmortem = usePublishPostmortem(postmortemId);
  const deletePostmortem = useDeletePostmortem();
  const linkIncident = useLinkIncident(postmortemId);
  const unlinkIncident = useUnlinkIncident(postmortemId);
  const addTimelineEntry = useAddTimelineEntry(postmortemId);
  const updateTimelineEntry = useUpdateTimelineEntry(postmortemId);
  const deleteTimelineEntry = useDeleteTimelineEntry(postmortemId);

  if (isLoading) {
    return (
      <>
        <PageHeader
          breadcrumbs={[
            { label: "Postmortems", onClick: () => navigate(ROUTES.POSTMORTEMS.LIST) },
            { label: "Loading…" },
          ]}
        />
        <PostmortemDetailSkeleton />
      </>
    );
  }

  if (isError || !postmortem) {
    return (
      <>
        <PageHeader
          breadcrumbs={[
            { label: "Postmortems", onClick: () => navigate(ROUTES.POSTMORTEMS.LIST) },
            { label: "Not found" },
          ]}
        />
        <div className="rounded-xl border bg-card px-6 py-8 text-sm text-destructive">
          Failed to load this postmortem.
        </div>
      </>
    );
  }

  const isPublished = postmortem.status === "Published";

  async function handleSaveFields(fields: PostmortemFieldValueUpdate[]) {
    try {
      await updatePostmortem.mutateAsync({
        name: null,
        reviewOwnerUserId: null,
        impactStartAt: null,
        impactEndAt: null,
        fields,
      });
      toast.success("Analysis saved.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to save analysis."));
    }
  }

  async function handleTogglePublish() {
    try {
      await publishPostmortem.mutateAsync(!isPublished);
      toast.success(isPublished ? "Reverted to draft." : "Review finalized.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to change status."));
    }
  }

  async function handleDelete() {
    const ok = await confirm({
      title: "Delete this postmortem?",
      description: "This removes the review report. Referenced incidents are not affected.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (!ok) return;
    try {
      await deletePostmortem.mutateAsync(postmortemId);
      toast.success("Postmortem deleted.");
      navigate(ROUTES.POSTMORTEMS.LIST);
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to delete postmortem."));
    }
  }

  async function handleLink(incidentId: number) {
    try {
      await linkIncident.mutateAsync(incidentId);
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to link incident."));
    }
  }

  async function handleUnlink(incidentId: number) {
    try {
      await unlinkIncident.mutateAsync(incidentId);
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to unlink incident."));
    }
  }

  async function handleAddNote(occurredAt: string, body: string) {
    try {
      await addTimelineEntry.mutateAsync({ occurredAt, body });
      toast.success("Note added.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to add note."));
    }
  }

  async function handleEditNote(entryId: number, occurredAt: string, body: string) {
    try {
      await updateTimelineEntry.mutateAsync({ entryId, data: { occurredAt, body } });
      toast.success("Note updated.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to update note."));
    }
  }

  async function handleDeleteNote(entryId: number) {
    const ok = await confirm({
      title: "Delete this note?",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (!ok) return;
    try {
      await deleteTimelineEntry.mutateAsync(entryId);
      toast.success("Note deleted.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to delete note."));
    }
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Postmortems", onClick: () => navigate(ROUTES.POSTMORTEMS.LIST) },
          { label: postmortem.name },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleTogglePublish}
              disabled={publishPostmortem.isPending}
            >
              {isPublished ? (
                <>
                  <Undo2 size={13} /> Revert to draft
                </>
              ) : (
                <>
                  <Send size={13} /> Finalize
                </>
              )}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={handleDelete}
              disabled={deletePostmortem.isPending}
            >
              <Trash2 size={13} /> Delete
            </Button>
          </div>
        }
      />

      <div className="mb-6 flex items-center gap-3">
        <h1 className="text-xl font-bold">{postmortem.name}</h1>
        <PostmortemStatusBadge status={postmortem.status} />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="flex flex-col gap-6 lg:col-span-2">
          <PostmortemFieldsSection
            key={postmortem.updatedAt}
            postmortem={postmortem}
            saving={updatePostmortem.isPending}
            onSave={handleSaveFields}
          />
          <PostmortemTimelineSection
            postmortem={postmortem}
            saving={
              addTimelineEntry.isPending ||
              updateTimelineEntry.isPending ||
              deleteTimelineEntry.isPending
            }
            onAdd={handleAddNote}
            onEdit={handleEditNote}
            onDelete={handleDeleteNote}
          />
        </div>

        <div className="flex flex-col gap-6">
          <div className="rounded-xl border bg-card p-5 text-sm">
            <h2 className="mb-3 text-sm font-semibold">Details</h2>
            <dl className="flex flex-col gap-2">
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Owner</dt>
                <dd>{postmortem.reviewOwnerName ?? "Unassigned"}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Created</dt>
                <dd>{formatDate(postmortem.createdAt)}</dd>
              </div>
              {postmortem.publishedAt && (
                <div className="flex justify-between">
                  <dt className="text-muted-foreground">Finalized</dt>
                  <dd>{formatDate(postmortem.publishedAt)}</dd>
                </div>
              )}
            </dl>
          </div>

          <PostmortemIncidentsSection
            postmortem={postmortem}
            linking={linkIncident.isPending || unlinkIncident.isPending}
            onLink={handleLink}
            onUnlink={handleUnlink}
          />
        </div>
      </div>
    </>
  );
}

export default PostmortemDetailPage;
