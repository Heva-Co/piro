import { useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, CheckCheck, Save, Eye, EyeOff, Globe, Lock, MessageSquare, Blend, AlertTriangle, Plus, PlusCircle, MinusCircle, FlagTriangleRight, ArrowRightLeft } from "lucide-react";
import { marked } from "marked";
import { PageHeader } from "@/components/PageHeader";
import ActionButtons from "@/components/integration-actions/ActionButtons";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone/DangerZone";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Marker, MarkerIcon, MarkerContent } from "@/components/ui/marker";
import { useAllServices } from "@/hooks/useServices";
import { incidentsApi } from "@/lib/actions/incidents";
import type { IncidentTimelineEvent } from "@/lib/actions/incidents";
import type { IncidentVisibilityKey } from "@/constants/incidents";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { IMPACT_OPTIONS } from "@/constants/serviceStatus";

const STATUS_BADGE: Record<string, string> = {
  INVESTIGATING: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  IDENTIFIED:    "bg-orange-500/15 text-orange-600 dark:text-orange-400",
  MONITORING:    "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  RESOLVED:      "bg-green-500/15 text-green-600 dark:text-green-400",
  MERGED:        "bg-violet-500/15 text-violet-600 dark:text-violet-400",
};

const STATUS_SELECT_LABEL: Record<string, string> = {
  __NO_CHANGE__: "No status change",
  INVESTIGATING: "Investigating",
  IDENTIFIED: "Identified",
  MONITORING: "Monitoring",
  RESOLVED: "Resolved",
};

const STATUS_FLOW_ORDER = ["INVESTIGATING", "IDENTIFIED", "MONITORING", "RESOLVED"];

interface ServiceImpact {
  slug: string;
  impact: string;
}

const SYSTEM_EVENT_ICON: Record<string, React.ReactNode> = {
  Created: <FlagTriangleRight />,
  StatusChanged: <ArrowRightLeft />,
  Acknowledged: <CheckCheck />,
  ServiceAdded: <PlusCircle />,
  ServiceRemoved: <MinusCircle />,
  MergedTo: <Blend />,
  MergedFrom: <Blend />,
  Published: <Eye />,
  Unpublished: <EyeOff />,
  AlertFired: <AlertTriangle />,
};

function describeSystemEvent(e: IncidentTimelineEvent): React.ReactNode {
  switch (e.type) {
    case "Created":
      return "Incident created";
    case "StatusChanged":
      return `Status changed from ${e.oldStatus} to ${e.newStatus}`;
    case "Acknowledged":
      return `Acknowledged by ${e.actorName}`;
    case "ServiceAdded":
      return "Service added";
    case "ServiceRemoved":
      return "Service removed";
    case "MergedTo":
      return (
        <>
          Merged into incident{" "}
          <Link to={ROUTES.INCIDENTS.TIMELINE(e.relatedIncidentId!)} className="font-semibold underline hover:no-underline">
            #{e.relatedIncidentId}
          </Link>
        </>
      );
    case "MergedFrom":
      return (
        <>
          Absorbed incident{" "}
          <Link to={ROUTES.INCIDENTS.TIMELINE(e.relatedIncidentId!)} className="font-semibold underline hover:no-underline">
            #{e.relatedIncidentId}
          </Link>
        </>
      );
    case "Published":
      return "Published to status page";
    case "Unpublished":
      return "Unpublished from status page";
    case "AlertFired":
      return e.alertId != null ? (
        <>
          Alert attached{" "}
          <Link to={ROUTES.ALERTS.DETAIL(e.alertId)} className="font-semibold underline hover:no-underline">
            #{e.alertId}
          </Link>
        </>
      ) : (
        "Alert attached"
      );
    default:
      return e.type;
  }
}

export default function IncidentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const { formatTimestamp } = useFormattedDate();
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

  function isServiceSelected(slug: string) {
    return serviceImpacts.some((s) => s.slug === slug);
  }

  function toggleService(slug: string) {
    setServiceImpacts((prev) =>
      prev.some((s) => s.slug === slug)
        ? prev.filter((s) => s.slug !== slug)
        : [...prev, { slug, impact: "DEGRADED" }]
    );
  }

  function setImpact(slug: string, impact: string) {
    setServiceImpacts((prev) =>
      prev.map((s) => (s.slug === slug ? { ...s, impact } : s))
    );
  }

  // ── Comment form ────────────────────────────────────────────────────────────
  const NO_STATUS_CHANGE = "__NO_CHANGE__";
  const [postDialogOpen, setPostDialogOpen] = useState(false);
  const [commentBody, setCommentBody] = useState("");
  const [commentStatus, setCommentStatus] = useState(NO_STATUS_CHANGE);
  const [commentVisibility, setCommentVisibility] = useState<IncidentVisibilityKey>("Private");
  const [commentError, setCommentError] = useState("");

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

  const isPublic = incident?.visibility === "Public";

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
      <>
        <div className="text-sm text-muted-foreground">Loading…</div>
      </>
    );
  }
  if (!incident) {
    return (
      <>
        <div className="text-sm text-destructive">Incident not found.</div>
      </>
    );
  }

  // Backend already returns events most-recent-first.
  const recentTimeline = timelinePage?.items ?? [];
  const timelineTotalCount = timelinePage?.totalCount ?? 0;
  const hiddenTimelineCount = timelineTotalCount - recentTimeline.length;
  const isResolved = incident.status === "Resolved" || incident.isResolved;
  const isMerged = incident.status === "Merged";
  const currentStatusUpper = incident.status?.toUpperCase() ?? "";
  const isBackwardStatusChange =
    commentStatus !== NO_STATUS_CHANGE &&
    STATUS_FLOW_ORDER.indexOf(commentStatus) < STATUS_FLOW_ORDER.indexOf(currentStatusUpper);

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Incidents", onClick: () => navigate(ROUTES.INCIDENTS.LIST) },
          { label: `#${incident.id}` },
        ]}
        actions={
          <>
            <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATUS_BADGE[incident.status?.toUpperCase()] ?? "bg-muted text-muted-foreground"}`}>
              {incident.status}
            </span>
            {isPublic ? (
              <span className="flex items-center gap-1 text-xs text-green-600 dark:text-green-400">
                <Globe size={12} /> Public
              </span>
            ) : (
              <span className="flex items-center gap-1 text-xs text-muted-foreground">
                <Lock size={12} /> Private
              </span>
            )}
            {!isResolved && (
              incident.acknowledgedAt ? (
                <div className="flex items-center gap-1.5 rounded-lg bg-green-500/10 border border-green-500/30 px-3 py-2 text-xs text-green-600 dark:text-green-400">
                  <CheckCheck size={13} />
                  <span>Acked by <strong>{incident.acknowledgedBy}</strong></span>
                </div>
              ) : (
                <Button
                  onClick={() => acknowledgeMutation.mutate()}
                  disabled={acknowledgeMutation.isPending}
                  variant="outline"
                >
                  <CheckCheck size={13} />
                  {acknowledgeMutation.isPending ? "…" : "Acknowledge"}
                </Button>
              )
            )}
            <ActionButtons context="Incident" targetId={incident.id} />
          </>
        }
      />

      <div className="rounded-xl border border-border bg-card px-6 py-5 mb-4">
        <input
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          onBlur={() => { if (hasTitleChanged) saveTitleMutation.mutate(); }}
          disabled={isResolved}
          className="text-xl font-bold bg-transparent border-0 border-b border-transparent hover:border-border focus:border-foreground/40 focus:outline-none w-full transition-colors disabled:hover:border-transparent"
        />
        <div className="flex items-center gap-2 mt-2 flex-wrap text-xs text-muted-foreground">
          <span>Started {formatTimestamp(incident.startDateTime)}</span>
          {incident.endDateTime && <span>· Resolved {formatTimestamp(incident.endDateTime)}</span>}
        </div>
      </div>

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
        <div className="rounded-xl border border-border bg-card overflow-hidden">
          {recentTimeline.length === 0 ? (
            <p className="px-5 py-8 text-center text-sm text-muted-foreground">No events yet.</p>
          ) : (
            <div>
              {recentTimeline.map((e, i) => {
                const prev = recentTimeline[i - 1];
                const needsTopBorder = i > 0 && prev.type === "CommentPosted" && e.type === "CommentPosted";

                if (e.type === "CommentPosted") {
                  return (
                    <div
                      key={e.id}
                      className={`px-5 py-4 flex gap-3 ${needsTopBorder ? "border-t border-border" : ""}`}
                    >
                      <div className="flex-1 flex flex-col gap-1.5">
                        <div className="flex items-center gap-2 text-xs text-muted-foreground">
                          <span>{formatTimestamp(new Date(e.occurredAt).getTime() / 1000)}</span>
                          {e.visibility === "Public" && (
                            <span className="flex items-center gap-1 text-green-600 dark:text-green-400">
                              <Globe size={11} /> Public
                            </span>
                          )}
                        </div>
                        <div
                          className="text-sm prose prose-sm max-w-none"
                          dangerouslySetInnerHTML={{ __html: marked(e.comment ?? "", { async: false }) as string }}
                        />
                      </div>
                      {!isResolved && (
                        <button
                          onClick={() => { if (confirm("Delete this update?")) deleteCommentMutation.mutate(e.id); }}
                          className="shrink-0 rounded p-1 text-muted-foreground/40 hover:text-destructive hover:bg-destructive/10 transition-colors"
                        >
                          ×
                        </button>
                      )}
                    </div>
                  );
                }

                return (
                  <div key={e.id} className="px-5 py-3">
                    <Marker variant="separator">
                      <MarkerContent className="flex items-center gap-1.5">
                        <MarkerIcon>{SYSTEM_EVENT_ICON[e.type] ?? <FlagTriangleRight />}</MarkerIcon>
                        {describeSystemEvent(e)}
                        <span className="text-muted-foreground/70">· {formatTimestamp(new Date(e.occurredAt).getTime() / 1000)}</span>
                      </MarkerContent>
                    </Marker>
                  </div>
                );
              })}
            </div>
          )}
          {hiddenTimelineCount > 0 && (
            <button
              onClick={() => navigate(ROUTES.INCIDENTS.TIMELINE(incident.id))}
              className="w-full px-5 py-3 text-center text-xs text-muted-foreground hover:text-foreground hover:bg-muted/30 transition-colors border-t border-border"
            >
              +{hiddenTimelineCount} more event{hiddenTimelineCount === 1 ? "" : "s"} — view full timeline
            </button>
          )}
        </div>
      </SectionAccordion>

      <Dialog open={postDialogOpen} onOpenChange={setPostDialogOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Post Update</DialogTitle>
            <DialogDescription>Add a status update to this incident's timeline.</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            {commentError && (
              <div className="flex items-center gap-2 text-sm text-destructive">
                <AlertCircle size={14} /> {commentError}
              </div>
            )}
            <Select value={commentStatus} onValueChange={(v) => v && setCommentStatus(v)}>
              <SelectTrigger className="w-56">
                <SelectValue>{(value: string | null) => STATUS_SELECT_LABEL[value ?? ""] ?? value}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_STATUS_CHANGE}>No status change</SelectItem>
                {STATUS_FLOW_ORDER.map((s) => (
                  <SelectItem key={s} value={s} disabled={s === currentStatusUpper}>
                    {STATUS_SELECT_LABEL[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {isBackwardStatusChange && (
              <div className="flex items-center gap-2 rounded-lg bg-yellow-500/10 border border-yellow-500/30 px-3 py-2 text-xs text-yellow-700 dark:text-yellow-500">
                <AlertTriangle size={14} className="shrink-0" />
                <span>
                  This moves the incident backward, from <strong>{STATUS_SELECT_LABEL[currentStatusUpper]}</strong> to{" "}
                  <strong>{STATUS_SELECT_LABEL[commentStatus]}</strong>.
                </span>
              </div>
            )}
            <MarkdownEditor
              value={commentBody}
              onChange={setCommentBody}
              placeholder="Describe the current situation… (optional — you can post a status change alone)"
            />
            {isPublic ? (
              <label className="flex items-center gap-2 text-xs text-muted-foreground cursor-pointer select-none">
                <Switch
                  checked={commentVisibility === "Public"}
                  onCheckedChange={(checked) => setCommentVisibility(checked ? "Public" : "Private")}
                />
                {commentVisibility === "Public" ? (
                  <span className="flex items-center gap-1 text-foreground font-medium"><Globe size={12} /> Visible on status page</span>
                ) : (
                  <span className="flex items-center gap-1"><Lock size={12} /> Internal only</span>
                )}
              </label>
            ) : (
              <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <Lock size={12} /> This incident is private — updates stay internal until published.
              </span>
            )}
          </div>
          <DialogFooter>
            <Button
              onClick={() => addCommentMutation.mutate()}
              disabled={
                addCommentMutation.isPending ||
                (!commentBody.trim() &&
                  (commentStatus === NO_STATUS_CHANGE || commentStatus === incident.status?.toUpperCase()))
              }
            >
              {addCommentMutation.isPending ? "Posting…" : "Post Update"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Impact ── */}
      <SectionAccordion
        title={`Impact (${incident.services?.length ?? 0})`}
        description="Scope of services affected by this incident"
        icon={<Blend size={16} className="text-muted-foreground" />}
        disableCard
      >
        <div className="rounded-xl border border-border bg-card overflow-hidden">
          <div className="divide-y divide-border">
            {allServices.length === 0 ? (
              <p className="px-5 py-8 text-center text-sm text-muted-foreground">No services found.</p>
            ) : (
              allServices.map((svc) => {
                const selected = isServiceSelected(svc.slug);
                const impact = serviceImpacts.find((s) => s.slug === svc.slug)?.impact ?? "DEGRADED";
                const triggeringCheckSlug = incident.services?.find((s) => s.serviceSlug === svc.slug)?.triggeringCheckSlug;
                return (
                  <div key={svc.slug} className="flex items-center gap-4 px-5 py-3">
                    <input
                      type="checkbox"
                      id={`svc-${svc.slug}`}
                      checked={selected}
                      onChange={() => toggleService(svc.slug)}
                      disabled={isResolved}
                      className="size-4 rounded border-border accent-foreground cursor-pointer"
                    />
                    <label htmlFor={`svc-${svc.slug}`} className="flex-1 text-sm font-medium cursor-pointer select-none">
                      {svc.name}
                      <span className="ml-2 text-xs text-muted-foreground font-normal">{svc.slug}</span>
                      {triggeringCheckSlug && (
                        <span className="ml-2 text-xs text-muted-foreground font-normal">
                          · triggered by <span className="font-mono">{triggeringCheckSlug}</span>
                        </span>
                      )}
                    </label>
                    {selected && (
                      <Select value={impact} onValueChange={(v) => v && setImpact(svc.slug, v)} disabled={isResolved}>
                        <SelectTrigger className="w-40 h-8 text-xs">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {IMPACT_OPTIONS.map((opt) => (
                            <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  </div>
                );
              })
            )}
          </div>

          {!isResolved && (
            <div className="flex items-center justify-between gap-4 px-5 py-3 border-t border-border bg-muted/30">
              <div>
                {impactError && (
                  <p className="text-xs text-destructive flex items-center gap-1">
                    <AlertCircle size={12} /> {impactError}
                  </p>
                )}
              </div>
              <Button onClick={() => saveImpactMutation.mutate()} disabled={!hasImpactChanged || saveImpactMutation.isPending}>
                <Save size={14} />
                {saveImpactMutation.isPending ? "Saving…" : "Save Impact"}
              </Button>
            </div>
          )}
        </div>
      </SectionAccordion>

      {/* ── Visibility ── */}
      <SectionAccordion
        title="Visibility"
        description={isPublic ? "Visible on the public status page" : "Private — not visible on the status page"}
        icon={isPublic ? <Globe size={16} className="text-muted-foreground" /> : <Lock size={16} className="text-muted-foreground" />}
        disableCard
      >
        {!isPublic ? (
          <div className="rounded-xl border border-yellow-500/30 bg-yellow-500/10 p-5 flex items-center justify-between gap-3">
            <p className="text-xs text-yellow-700 dark:text-yellow-500">This incident is private. Publish it to make it (and any Public updates) visible on the status page.</p>
            {!isMerged && (
              <Button onClick={() => publishMutation.mutate()} disabled={publishMutation.isPending}>
                <Eye size={12} /> {publishMutation.isPending ? "Publishing…" : "Publish Now"}
              </Button>
            )}
          </div>
        ) : (
          <div className="rounded-xl border border-green-500/30 bg-green-500/10 p-5 flex items-center justify-between gap-3">
            <p className="text-xs text-green-700 dark:text-green-500">This incident and its public updates are visible on the status page.</p>
            {!isMerged && (
              <Button
                variant="outline"
                onClick={() => { if (confirm("Unpublish this incident? It will be hidden from the status page and all its public updates will become internal.")) unpublishMutation.mutate(); }}
                disabled={unpublishMutation.isPending}
              >
                <EyeOff size={12} /> {unpublishMutation.isPending ? "Unpublishing…" : "Unpublish"}
              </Button>
            )}
          </div>
        )}
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
    </>
  );
}
