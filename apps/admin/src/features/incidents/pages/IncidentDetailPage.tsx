import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, CheckCheck, Save, Eye, EyeOff, Globe, Lock, MessageSquare, Blend, AlertTriangle } from "lucide-react";
import { marked } from "marked";
import { PageHeader } from "@/components/PageHeader";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone/DangerZone";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { servicesApi } from "@/lib/api";
import { incidentsApi } from "@/lib/actions/incidents";
import type { IncidentVisibilityKey } from "@/constants/incidents";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { formatTimestamp } from "@/utils/date";
import { IMPACT_OPTIONS } from "@/constants/serviceStatus";

const STATUS_BADGE: Record<string, string> = {
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

  // ── Impact state ──────────────────────────────────────────────────
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
  const [commentStatus, setCommentStatus] = useState("INVESTIGATING");
  const [commentVisibility, setCommentVisibility] = useState<IncidentVisibilityKey>("Private");
  const [commentError, setCommentError] = useState("");

  // ── Mutations ───────────────────────────────────────────────────────────────
  const addCommentMutation = useMutation({
    mutationFn: () => incidentsApi.addComment(id!, commentBody, commentStatus, commentVisibility),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: incidentKey });
      setCommentBody("");
      setCommentVisibility("Private");
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

  const comments = incident.comments ?? [];
  const isResolved = incident.status === "Resolved" || incident.isResolved;

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
        {incident.mergedIntoIncidentId && (
          <div className="mt-3 rounded-lg bg-purple-500/10 px-3 py-2 text-xs text-purple-700 dark:text-purple-400">
            This incident was merged into{" "}
            <button
              onClick={() => navigate(ROUTES.INCIDENTS.DETAIL(incident.mergedIntoIncidentId!))}
              className="font-semibold underline hover:no-underline"
            >
              #{incident.mergedIntoIncidentId}
            </button>
          </div>
        )}
      </div>

      {/* ── Updates ── */}
      <SectionAccordion
        title={`Updates (${comments.length})`}
        description="Status updates and internal notes"
        icon={<MessageSquare size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <div className="rounded-xl border border-border bg-card overflow-hidden">
          {!isResolved && (
            <div className="px-5 py-4 border-b border-border flex flex-col gap-3 bg-muted/30">
              <h3 className="text-sm font-semibold">Post Update</h3>
              {commentError && (
                <div className="flex items-center gap-2 text-sm text-destructive">
                  <AlertCircle size={14} /> {commentError}
                </div>
              )}
              <Select value={commentStatus} onValueChange={(v) => v && setCommentStatus(v)}>
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
              <div className="flex items-center justify-between gap-3">
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
                <Button onClick={() => addCommentMutation.mutate()} disabled={!commentBody.trim() || addCommentMutation.isPending} className="shrink-0">
                  {addCommentMutation.isPending ? "Posting…" : "Post Update"}
                </Button>
              </div>
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
                      <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium capitalize ${STATUS_BADGE[c.status?.toUpperCase()] ?? "bg-muted text-muted-foreground"}`}>
                        {c.status}
                      </span>
                      <span className="text-xs text-muted-foreground">{formatTimestamp(c.commentedAt)}</span>
                      {c.visibility === "Public" ? (
                        <span className="flex items-center gap-1 text-xs text-green-600 dark:text-green-400">
                          <Globe size={11} /> Public
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 text-xs text-muted-foreground">
                          <Lock size={11} /> Internal
                        </span>
                      )}
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
        </div>
      </SectionAccordion>

      {/* ── Impact ── */}
      <SectionAccordion
        title={`Impact (${incident.isGlobal ? "Global" : (incident.services?.length ?? 0)})`}
        description="Scope of services affected by this incident"
        icon={<Blend size={16} className="text-muted-foreground" />}
      >
        <div className="rounded-xl border border-border bg-card overflow-hidden">
          <div className="px-5 py-4 border-b border-border flex items-center justify-between gap-4">
            <div>
              <p className="text-sm font-semibold">Global Incident</p>
              <p className="text-xs text-muted-foreground mt-0.5">
                Enable this if the incident affects the entire platform, regardless of individual services.
              </p>
            </div>
            <Switch checked={isGlobal} onCheckedChange={setIsGlobal} disabled={isResolved} />
          </div>

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
                      <label htmlFor={`svc-${svc.slug}`} className="flex-1 text-sm font-medium cursor-pointer select-none">
                        {svc.name}
                        <span className="ml-2 text-xs text-muted-foreground font-normal">{svc.slug}</span>
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
          )}

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
      >
        {!isPublic ? (
          <div className="rounded-xl border border-yellow-500/30 bg-yellow-500/10 p-5 flex items-center justify-between gap-3">
            <p className="text-xs text-yellow-700 dark:text-yellow-500">This incident is private. Publish it to make it (and any Public updates) visible on the status page.</p>
            <Button onClick={() => publishMutation.mutate()} disabled={publishMutation.isPending}>
              <Eye size={12} /> {publishMutation.isPending ? "Publishing…" : "Publish Now"}
            </Button>
          </div>
        ) : (
          <div className="rounded-xl border border-green-500/30 bg-green-500/10 p-5 flex items-center justify-between gap-3">
            <p className="text-xs text-green-700 dark:text-green-500">This incident and its public updates are visible on the status page.</p>
            <Button
              variant="outline"
              onClick={() => { if (confirm("Unpublish this incident? It will be hidden from the status page and all its public updates will become internal.")) unpublishMutation.mutate(); }}
              disabled={unpublishMutation.isPending}
            >
              <EyeOff size={12} /> {unpublishMutation.isPending ? "Unpublishing…" : "Unpublish"}
            </Button>
          </div>
        )}
      </SectionAccordion>

      {/* ── Danger Zone ── */}
      <SectionAccordion
        title="Danger Zone"
        description="Irreversible actions for this incident"
        icon={<AlertTriangle size={16} className="text-destructive" />}
        titleClassName="text-destructive"
      >
        <DangerZone objectName="incident" objectId={String(incident.id)} onDelete={handleDelete} />
      </SectionAccordion>
    </>
  );
}
