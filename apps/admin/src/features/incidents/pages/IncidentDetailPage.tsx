import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, ChevronLeft, CheckCheck, Save, Eye, Clock, X } from "lucide-react";
import { marked } from "marked";
import { AdminLayout } from "@/components/AdminLayout";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { incidentsApi, servicesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { formatTimestamp } from "@/utils/date";
import { IMPACT_OPTIONS } from "@/constants/serviceStatus";

const STATE_BADGE: Record<string, string> = {
  INVESTIGATING: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  IDENTIFIED:    "bg-orange-500/15 text-orange-600 dark:text-orange-400",
  MONITORING:    "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  RESOLVED:      "bg-green-500/15 text-green-600 dark:text-green-400",
};

interface ServiceImpact {
  slug: string;
  impact: string;
}

export default function IncidentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const incidentKey = QUERY_KEYS.INCIDENT(id!);

  const { data: incident, isLoading } = useQuery({
    queryKey: incidentKey,
    queryFn: () => incidentsApi.get(id!),
  });

  const { data: allServices = [] } = useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });

  // ── Title edit state ────────────────────────────────────────────────────────
  const [title, setTitle] = useState("");
  const [titleInit, setTitleInit] = useState(false);
  if (incident && !titleInit) {
    setTitle(incident.title);
    setTitleInit(true);
  }
  const hasTitleChanged = titleInit && incident ? title !== incident.title : false;

  // ── Impact tab state ──────────────────────────────────────────────────
  const [isGlobal, setIsGlobal] = useState(false);
  const [serviceImpacts, setServiceImpacts] = useState<ServiceImpact[]>([]);
  const [impactInit, setImpactInit] = useState(false);
  if (incident && !impactInit) {
    setIsGlobal(incident.isGlobal);
    setServiceImpacts(
      incident.services?.map((s) => ({ slug: s.serviceSlug, impact: s.impact })) ?? []
    );
    setImpactInit(true);
  }
  const [impactError, setImpactError] = useState("");

  const hasImpactChanged = impactInit && incident
    ? isGlobal !== incident.isGlobal ||
      JSON.stringify([...serviceImpacts].sort((a, b) => a.slug.localeCompare(b.slug))) !==
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
  const [commentBody, setCommentBody] = useState("");
  const [commentState, setCommentState] = useState("INVESTIGATING");
  const [commentError, setCommentError] = useState("");

  // ── Mutations ───────────────────────────────────────────────────────────────
  const addCommentMutation = useMutation({
    mutationFn: () => incidentsApi.addComment(id!, commentBody, commentState),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: incidentKey });
      setCommentBody("");
      setCommentError("");
    },
    onError: () => setCommentError("Failed to add update."),
  });

  const deleteCommentMutation = useMutation({
    mutationFn: (commentId: number) => incidentsApi.deleteComment(id!, commentId),
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
      await incidentsApi.update(id!, { isGlobal });
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

  const publishKey = ["incident-publish-schedule", id];

  const { data: publishSchedule } = useQuery({
    queryKey: publishKey,
    queryFn: () => incidentsApi.getPublishSchedule(id!),
    enabled: Boolean(id) && !incident?.isPublic,
    refetchInterval: 30_000,
  });

  const publishMutation = useMutation({
    mutationFn: () => incidentsApi.publish(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: incidentKey });
      qc.invalidateQueries({ queryKey: publishKey });
    },
  });

  const delayPublishMutation = useMutation({
    mutationFn: (mins: number) => incidentsApi.delayPublish(id!, mins),
    onSuccess: () => qc.invalidateQueries({ queryKey: publishKey }),
  });

  const cancelPublishMutation = useMutation({
    mutationFn: () => incidentsApi.cancelPublish(id!),
    onSuccess: () => qc.invalidateQueries({ queryKey: publishKey }),
  });

  // ── Render ──────────────────────────────────────────────────────────────────
  if (isLoading) {
    return <AdminLayout title="Incident"><p className="text-muted-foreground">Loading…</p></AdminLayout>;
  }
  if (!incident) {
    return <AdminLayout title="Incident"><p className="text-destructive">Incident not found.</p></AdminLayout>;
  }

  const comments = incident.comments ?? [];
  const isResolved = incident.state === "Resolved" || incident.isResolved;

  return (
    <AdminLayout title={`Incident #${incident.id}`}>
      <div className="flex flex-col gap-5">
        {/* Back */}
        <button
          onClick={() => navigate(ROUTES.INCIDENTS.LIST)}
          className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground self-start"
        >
          <ChevronLeft size={16} /> Back to Incidents
        </button>

        {/* Details card */}
        <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
          <div className="px-6 py-5 flex items-start justify-between gap-4">
            <div className="flex flex-col gap-1 flex-1 min-w-0">
              <span className="text-xs text-muted-foreground font-mono">#{incident.id}</span>
              <input
                type="text"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                onBlur={() => { if (hasTitleChanged) saveTitleMutation.mutate(); }}
                disabled={isResolved}
                className="text-xl font-bold bg-transparent border-0 border-b border-transparent hover:border-border focus:border-foreground/40 focus:outline-none w-full transition-colors disabled:hover:border-transparent"
              />
              <div className="flex items-center gap-2 mt-1 flex-wrap">
                <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATE_BADGE[incident.state?.toUpperCase()] ?? "bg-muted text-muted-foreground"}`}>
                  {incident.state}
                </span>
                <span className="text-xs text-muted-foreground">
                  Started {formatTimestamp(incident.startDateTime)}
                </span>
                {incident.endDateTime && (
                  <span className="text-xs text-muted-foreground">
                    · Resolved {formatTimestamp(incident.endDateTime)}
                  </span>
                )}
              </div>
            </div>

            {/* Acknowledge */}
            {!isResolved && (
              <div className="shrink-0">
                {incident.acknowledgedAt ? (
                  <div className="flex items-center gap-1.5 rounded-lg bg-green-500/10 border border-green-500/30 px-3 py-2 text-xs text-green-600 dark:text-green-400">
                    <CheckCheck size={13} />
                    <span>Acked by <strong>{incident.acknowledgedBy}</strong></span>
                  </div>
                ) : (
                  <button
                    onClick={() => acknowledgeMutation.mutate()}
                    disabled={acknowledgeMutation.isPending}
                    className="flex items-center gap-1.5 rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-xs font-medium text-amber-600 dark:text-amber-400 hover:bg-amber-500/20 disabled:opacity-50 transition-colors"
                  >
                    <CheckCheck size={13} />
                    {acknowledgeMutation.isPending ? "…" : "Acknowledge"}
                  </button>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Tabs */}
        <Tabs defaultValue="updates">
          <TabsList>
            <TabsTrigger value="updates">Updates ({comments.length})</TabsTrigger>
            <TabsTrigger value="afectaciones">
              Impact ({incident.isGlobal ? "Global" : (incident.services?.length ?? 0)})
            </TabsTrigger>
            <TabsTrigger value="log" disabled>
              Log <span className="ml-1.5 text-[10px] bg-muted text-muted-foreground rounded px-1.5 py-0.5 font-normal">soon</span>
            </TabsTrigger>
          </TabsList>

          {/* ── Updates tab ── */}
          <TabsContent value="updates" className="rounded-xl border border-border bg-card overflow-hidden mt-0">
            {!isResolved && (
              <div className="px-5 py-4 border-b border-border flex flex-col gap-3 bg-muted/30">
                <h3 className="text-sm font-semibold">Post Update</h3>
                {commentError && (
                  <div className="flex items-center gap-2 text-sm text-destructive">
                    <AlertCircle size={14} /> {commentError}
                  </div>
                )}
                <Select value={commentState} onValueChange={(v) => v && setCommentState(v)}>
                  <SelectTrigger className="w-48">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="INVESTIGATING">Investigating</SelectItem>
                    <SelectItem value="IDENTIFIED">Identified</SelectItem>
                    <SelectItem value="MONITORING">Monitoring</SelectItem>
                    <SelectItem value="RESOLVED">Resolved</SelectItem>
                  </SelectContent>
                </Select>
                <MarkdownEditor
                  value={commentBody}
                  onChange={setCommentBody}
                  placeholder="Describe the current situation…"
                />
                <button
                  onClick={() => addCommentMutation.mutate()}
                  disabled={!commentBody.trim() || addCommentMutation.isPending}
                  className="self-start rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
                >
                  {addCommentMutation.isPending ? "Posting…" : "Post Update"}
                </button>
              </div>
            )}

            {/* Internal / publish banner */}
            {!incident.isPublic && !isResolved && (
              <div className="px-5 py-3 border-b border-border bg-yellow-500/10 flex flex-col gap-2">
                <div className="flex items-center gap-2">
                  <Eye size={14} className="text-yellow-600" />
                  <span className="text-xs font-semibold text-yellow-800 dark:text-yellow-400">Internal — not visible on status page</span>
                </div>
                {publishSchedule?.scheduledAt && (
                  <p className="text-xs text-yellow-700 dark:text-yellow-500">
                    Auto-publish at {new Date(publishSchedule.scheduledAt).toLocaleTimeString()}
                  </p>
                )}
                <div className="flex flex-wrap gap-2 pt-1">
                  <button
                    onClick={() => publishMutation.mutate()}
                    disabled={publishMutation.isPending}
                    className="flex items-center gap-1.5 rounded-md bg-yellow-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-yellow-700 disabled:opacity-50"
                  >
                    <Eye size={12} /> Publish Now
                  </button>
                  <button
                    onClick={() => delayPublishMutation.mutate(5)}
                    disabled={delayPublishMutation.isPending}
                    className="flex items-center gap-1.5 rounded-md border border-yellow-300 bg-background px-3 py-1.5 text-xs font-medium text-yellow-700 hover:bg-yellow-50 disabled:opacity-50"
                  >
                    <Clock size={12} /> +5 min
                  </button>
                  <button
                    onClick={() => delayPublishMutation.mutate(15)}
                    disabled={delayPublishMutation.isPending}
                    className="flex items-center gap-1.5 rounded-md border border-yellow-300 bg-background px-3 py-1.5 text-xs font-medium text-yellow-700 hover:bg-yellow-50 disabled:opacity-50"
                  >
                    <Clock size={12} /> +15 min
                  </button>
                  {publishSchedule?.scheduledAt && (
                    <button
                      onClick={() => cancelPublishMutation.mutate()}
                      disabled={cancelPublishMutation.isPending}
                      className="flex items-center gap-1.5 rounded-md border border-destructive/30 bg-background px-3 py-1.5 text-xs font-medium text-destructive hover:bg-destructive/10 disabled:opacity-50"
                    >
                      <X size={12} /> Cancel auto-publish
                    </button>
                  )}
                </div>
              </div>
            )}

            {/* Merged badge */}
            {incident.mergedIntoIncidentId && (
              <div className="px-5 py-3 border-b border-border bg-purple-500/10 text-xs text-purple-700 dark:text-purple-400">
                This incident was merged into{" "}
                <button
                  onClick={() => navigate(ROUTES.INCIDENTS.DETAIL(incident.mergedIntoIncidentId!))}
                  className="font-semibold underline hover:no-underline"
                >
                  #{incident.mergedIntoIncidentId}
                </button>
              </div>
            )}

            {comments.length === 0 ? (
              <p className="px-5 py-8 text-center text-sm text-muted-foreground">No updates yet.</p>
            ) : (
              <div className="divide-y divide-border">
                {[...comments].reverse().map((c) => (
                  <div key={c.id} className="px-5 py-4 flex gap-3">
                    <div className="flex-1 flex flex-col gap-1.5">
                      <div className="flex items-center gap-2">
                        <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium capitalize ${STATE_BADGE[c.state?.toUpperCase()] ?? "bg-muted text-muted-foreground"}`}>
                          {c.state}
                        </span>
                        <span className="text-xs text-muted-foreground">{formatTimestamp(c.commentedAt)}</span>
                      </div>
                      <div
                        className="text-sm prose prose-sm max-w-none"
                        dangerouslySetInnerHTML={{ __html: marked(c.comment ?? "", { async: false }) as string }}
                      />
                    </div>
                    {!isResolved && (
                      <button
                        onClick={() => { if (confirm("Delete this update?")) deleteCommentMutation.mutate(c.id); }}
                        className="shrink-0 rounded p-1 text-muted-foreground/40 hover:text-destructive hover:bg-destructive/10 transition-colors"
                      >
                        ×
                      </button>
                    )}
                  </div>
                ))}
              </div>
            )}
          </TabsContent>

          {/* ── Impact tab ── */}
          <TabsContent value="afectaciones" className="rounded-xl border border-border bg-card overflow-hidden mt-0">
            {/* Header with wording */}
            <div className="px-5 py-4 border-b border-border bg-muted/30">
              <h3 className="text-sm font-semibold">Scope of Impact</h3>
              <p className="mt-1 text-xs text-muted-foreground">
                Define whether this incident affects all services globally or select the specific services impacted and their severity level.
              </p>
            </div>

            {/* Global toggle */}
            <div className="px-5 py-4 border-b border-border flex items-center justify-between gap-4">
              <div>
                <p className="text-sm font-semibold">Global Incident</p>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Enable this if the incident affects the entire platform, regardless of individual services.
                </p>
              </div>
              <Switch
                checked={isGlobal}
                onCheckedChange={setIsGlobal}
                disabled={isResolved}
              />
            </div>

            {/* Services list */}
            {isGlobal ? (
              <p className="px-5 py-8 text-center text-sm text-muted-foreground italic">
                All services are affected — no individual selection needed.
              </p>
            ) : (
              <div className="divide-y divide-border">
                {allServices.length === 0 ? (
                  <p className="px-5 py-8 text-center text-sm text-muted-foreground">No services found.</p>
                ) : (
                  allServices.map((svc) => {
                    const selected = isServiceSelected(svc.slug);
                    const impact = serviceImpacts.find((s) => s.slug === svc.slug)?.impact ?? "DEGRADED";
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
                        <label
                          htmlFor={`svc-${svc.slug}`}
                          className="flex-1 text-sm font-medium cursor-pointer select-none"
                        >
                          {svc.name}
                          <span className="ml-2 text-xs text-muted-foreground font-normal">{svc.slug}</span>
                        </label>
                        {selected && (
                          <Select
                            value={impact}
                            onValueChange={(v) => v && setImpact(svc.slug, v)}
                            disabled={isResolved}
                          >
                            <SelectTrigger className="w-40 h-8 text-xs">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {IMPACT_OPTIONS.map((opt) => (
                                <SelectItem key={opt.value} value={opt.value}>
                                  {opt.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        )}
                      </div>
                    );
                  })
                )}
              </div>
            )}

            {/* Footer save */}
            {!isResolved && (
              <div className="flex items-center justify-between gap-4 px-5 py-3 border-t border-border bg-muted/30">
                <div>
                  {impactError && (
                    <p className="text-xs text-destructive flex items-center gap-1">
                      <AlertCircle size={12} /> {impactError}
                    </p>
                  )}
                </div>
                <button
                  onClick={() => saveImpactMutation.mutate()}
                  disabled={!hasImpactChanged || saveImpactMutation.isPending}
                  className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-40 transition-opacity"
                >
                  <Save size={14} />
                  {saveImpactMutation.isPending ? "Saving…" : "Save Impact"}
                </button>
              </div>
            )}
          </TabsContent>
        </Tabs>
      </div>
    </AdminLayout>
  );
}
