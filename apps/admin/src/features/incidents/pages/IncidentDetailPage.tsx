import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { MessageSquare, Blend, AlertTriangle, Plus, Globe, Lock } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import ActionButtons from "@/components/integration-actions/ActionButtons";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone/DangerZone";
import { Button } from "@/components/ui/button";
import { WarningConfirmDialog } from "@/components/ui/warning-confirm-dialog";
import { useAllServices } from "@/hooks/useServices";
import { incidentsApi } from "@/lib/actions/incidents";
import type { IncidentVisibilityKey } from "@/constants/incidents";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import IncidentStatusActions from "../components/IncidentStatusActions";
import IncidentTitleCard from "../components/IncidentTitleCard";
import IncidentTimelineSection from "../components/IncidentTimelineSection";
import PostUpdateDialog, { NO_STATUS_CHANGE } from "../components/PostUpdateDialog";
import IncidentImpactSection, { type ServiceImpact } from "../components/IncidentImpactSection";
import IncidentVisibilitySection from "../components/IncidentVisibilitySection";

function IncidentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const incidentKey = QUERY_KEYS.INCIDENT(id!);

  const { data: incident, isLoading } = useQuery({
    queryKey: incidentKey,
    queryFn: () => incidentsApi.get(id!),
  });

  // Timeline is fetched independently from the incident itself — GET /incidents/{id} never
  // embeds it, so this summary view (first 10 events) is its own query/cache entry.
  const timelineKey = [...incidentKey, "timeline", 1, 10] as const;
  const { data: timelinePage } = useQuery({
    queryKey: timelineKey,
    queryFn: () => incidentsApi.getTimeline(id!, 1, 10),
    enabled: !!id,
  });

  const { data: allServices = [] } = useAllServices();

  // ── Title edit state ────────────────────────────────────────────────────────
  const [title, setTitle] = useState("");
  const [titleInit, setTitleInit] = useState(false);
  if (incident && !titleInit) {
    setTitle(incident.title);
    setTitleInit(true);
  }
  const hasTitleChanged = titleInit && incident ? title !== incident.title : false;

  // ── Impact state ──────────────────────────────────────────────────
  const [serviceImpacts, setServiceImpacts] = useState<ServiceImpact[]>([]);
  const [impactInit, setImpactInit] = useState(false);
  if (incident && !impactInit) {
    setServiceImpacts(
      incident.services?.map((s) => ({ slug: s.serviceSlug, impact: s.impact })) ?? []
    );
    setImpactInit(true);
  }
  const [impactError, setImpactError] = useState("");

  const hasImpactChanged = impactInit && incident
    ? JSON.stringify([...serviceImpacts].sort((a, b) => a.slug.localeCompare(b.slug))) !==
      JSON.stringify(
        [...(incident.services?.map((s) => ({ slug: s.serviceSlug, impact: s.impact })) ?? [])]
          .sort((a, b) => a.slug.localeCompare(b.slug))
      )
    : false;

  function toggleService(slug: string) {
    setServiceImpacts((prev) =>
      prev.some((s) => s.slug === slug)
        ? prev.filter((s) => s.slug !== slug)
        : [...prev, { slug, impact: "DEGRADED" }]
    );
  }

  function setImpact(slug: string, impact: string) {
    setServiceImpacts((prev) => prev.map((s) => (s.slug === slug ? { ...s, impact } : s)));
  }

  // ── Comment form ────────────────────────────────────────────────────────────
  const [postDialogOpen, setPostDialogOpen] = useState(false);
  const [commentBody, setCommentBody] = useState("");
  const [commentStatus, setCommentStatus] = useState(NO_STATUS_CHANGE);
  const [commentVisibility, setCommentVisibility] = useState<IncidentVisibilityKey>("Private");
  const [commentError, setCommentError] = useState("");
  const [pendingDeleteEventId, setPendingDeleteEventId] = useState<number | null>(null);

  // ── Mutations ───────────────────────────────────────────────────────────────
  const addCommentMutation = useMutation({
    mutationFn: () =>
      incidentsApi.addTimelineComment(
        id!,
        commentBody,
        commentStatus === NO_STATUS_CHANGE ? null : commentStatus,
        commentVisibility
      ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: incidentKey });
      setCommentBody("");
      setCommentStatus(NO_STATUS_CHANGE);
      setCommentVisibility("Private");
      setCommentError("");
      setPostDialogOpen(false);
    },
    onError: () => setCommentError("Failed to add update."),
  });

  const deleteCommentMutation = useMutation({
    mutationFn: (eventId: number) => incidentsApi.deleteTimelineComment(id!, eventId),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKey }),
  });

  const acknowledgeMutation = useMutation({
    mutationFn: () => incidentsApi.acknowledge(id!),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKey }),
  });

  const saveTitleMutation = useMutation({
    mutationFn: () => incidentsApi.update(id!, { title }),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKey }),
  });

  const saveImpactMutation = useMutation({
    mutationFn: async () => {
      await incidentsApi.setServices(
        id!,
        serviceImpacts.map((s) => ({ serviceSlug: s.slug, impact: s.impact }))
      );
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: incidentKey });
      setImpactError("");
    },
    onError: () => setImpactError("Failed to save impact."),
  });

  const publishMutation = useMutation({
    mutationFn: () => incidentsApi.publish(id!),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKey }),
  });

  const unpublishMutation = useMutation({
    mutationFn: () => incidentsApi.unpublish(id!),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKey }),
  });

  const deleteMutation = useMutation({
    mutationFn: () => incidentsApi.delete(id!),
  });

  async function handleDelete() {
    await deleteMutation.mutateAsync();
    navigate(ROUTES.INCIDENTS.LIST);
  }

  // ── Render ──────────────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <PageContainer>
        <div className="text-sm text-muted-foreground">Loading…</div>
      </PageContainer>
    );
  }
  if (!incident) {
    return (
      <PageContainer>
        <div className="text-sm text-destructive">Incident not found.</div>
      </PageContainer>
    );
  }

  // Backend already returns events most-recent-first.
  const recentTimeline = timelinePage?.items ?? [];
  const timelineTotalCount = timelinePage?.totalCount ?? 0;
  const hiddenTimelineCount = timelineTotalCount - recentTimeline.length;
  const isResolved = incident.status === "Resolved" || incident.isResolved;
  const isMerged = incident.status === "Merged";
  const isPublic = incident.visibility === "Public";
  const currentStatusUpper = incident.status?.toUpperCase() ?? "";

  const postDisabled =
    !commentBody.trim() && (commentStatus === NO_STATUS_CHANGE || commentStatus === currentStatusUpper);

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[
          { label: "Incidents", onClick: () => navigate(ROUTES.INCIDENTS.LIST) },
          { label: `#${incident.id}` },
        ]}
        actions={
          <>
            <IncidentStatusActions
              incident={incident}
              isResolved={isResolved}
              isAcknowledging={acknowledgeMutation.isPending}
              onAcknowledge={() => acknowledgeMutation.mutate()}
            />
            <ActionButtons context="Incident" targetId={incident.id} />
          </>
        }
      />

      <IncidentTitleCard
        title={title}
        onTitleChange={setTitle}
        onCommit={() => { if (hasTitleChanged) saveTitleMutation.mutate(); }}
        disabled={isResolved}
        startDateTime={incident.startDateTime}
        endDateTime={incident.endDateTime}
      />

      {/* ── Timeline ── */}
      <SectionAccordion
        title={`Timeline (${timelineTotalCount})`}
        description="Status updates and lifecycle events"
        icon={<MessageSquare size={16} className="text-muted-foreground" />}
        defaultOpen
        actions={
          <>
            {!isResolved && (
              <Button size="sm" onClick={() => setPostDialogOpen(true)}>
                <Plus size={14} /> Post Update
              </Button>
            )}
            <Button variant="outline" size="sm" onClick={() => navigate(ROUTES.INCIDENTS.TIMELINE(incident.id))}>
              View full timeline
            </Button>
          </>
        }
        disableCard
      >
        <IncidentTimelineSection
          events={recentTimeline}
          hiddenCount={hiddenTimelineCount}
          isResolved={isResolved}
          onDeleteComment={setPendingDeleteEventId}
          onViewFull={() => navigate(ROUTES.INCIDENTS.TIMELINE(incident.id))}
        />
      </SectionAccordion>

      <PostUpdateDialog
        open={postDialogOpen}
        onOpenChange={setPostDialogOpen}
        isPublic={isPublic}
        currentStatusUpper={currentStatusUpper}
        error={commentError}
        body={commentBody}
        onBodyChange={setCommentBody}
        status={commentStatus}
        onStatusChange={setCommentStatus}
        visibility={commentVisibility}
        onVisibilityChange={setCommentVisibility}
        isSubmitting={addCommentMutation.isPending}
        submitDisabled={postDisabled}
        onSubmit={() => addCommentMutation.mutate()}
      />

      {/* ── Impact ── */}
      <SectionAccordion
        title={`Impact (${incident.services?.length ?? 0})`}
        description="Scope of services affected by this incident"
        icon={<Blend size={16} className="text-muted-foreground" />}
        disableCard
      >
        <IncidentImpactSection
          allServices={allServices}
          incidentServices={incident.services ?? []}
          serviceImpacts={serviceImpacts}
          isResolved={isResolved}
          impactError={impactError}
          hasImpactChanged={hasImpactChanged}
          isSaving={saveImpactMutation.isPending}
          onToggleService={toggleService}
          onSetImpact={setImpact}
          onSave={() => saveImpactMutation.mutate()}
        />
      </SectionAccordion>

      {/* ── Visibility ── */}
      <SectionAccordion
        title="Visibility"
        description={isPublic ? "Visible on the public status page" : "Private — not visible on the status page"}
        icon={isPublic ? <Globe size={16} className="text-muted-foreground" /> : <Lock size={16} className="text-muted-foreground" />}
        disableCard
      >
        <IncidentVisibilitySection
          isPublic={isPublic}
          isMerged={isMerged}
          isPublishing={publishMutation.isPending}
          isUnpublishing={unpublishMutation.isPending}
          onPublish={() => publishMutation.mutate()}
          onUnpublish={() => unpublishMutation.mutate()}
        />
      </SectionAccordion>

      {/* ── Danger Zone ── */}
      <SectionAccordion
        title="Danger Zone"
        description="Irreversible actions for this incident"
        icon={<AlertTriangle size={16} className="text-destructive" />}
        titleClassName="text-destructive"
        disableCard
      >
        <DangerZone objectName="incident" objectId={String(incident.id)} onDelete={handleDelete} />
      </SectionAccordion>

      <WarningConfirmDialog
        open={pendingDeleteEventId != null}
        onOpenChange={(open) => { if (!open) setPendingDeleteEventId(null); }}
        title="Delete this update?"
        description="This removes the update from the incident timeline. This can't be undone."
        confirmLabel="Delete"
        confirmPendingLabel="Deleting…"
        isPending={deleteCommentMutation.isPending}
        onConfirm={() => {
          if (pendingDeleteEventId != null) deleteCommentMutation.mutate(pendingDeleteEventId);
          setPendingDeleteEventId(null);
        }}
      />
    </PageContainer>
  );
}

export default IncidentDetailPage;
