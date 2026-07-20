import { useState } from "react";
import { Link, useParams, useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { toast } from "react-toastify";
import { ExternalLink, CheckCheck, Link2, PlusCircle, Info, ListChecks, Siren, History, XCircle, FileJson } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import ActionButtons from "@/components/integration-actions/ActionButtons";
import { StatusPill } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { useAlert } from "@/hooks/useChecks";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { alertsApi } from "@/lib/actions/alerts";
import { AlertSourceBadge } from "../components/AlertSourceBadge";
import EscalationHaltedBanner from "../components/EscalationHaltedBanner";
import { PayloadDialog } from "@/components/PayloadDialog";

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      <div className="text-sm">{children}</div>
    </div>
  );
}

export default function AlertDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const { data: alert, isLoading } = useAlert(id);
  const { formatDateTime } = useFormattedDate();

  function formatDate(value?: string) {
    if (!value) return "—";
    return formatDateTime(value);
  }

  const [attachOpen, setAttachOpen] = useState(false);
  const [selectedIncidentId, setSelectedIncidentId] = useState<string>("");
  const [payloadOpen, setPayloadOpen] = useState(false);

  const { data: openIncidents = [] } = useQuery({
    queryKey: ["alerts", "open-incidents"],
    queryFn: alertsApi.getOpenIncidents,
    enabled: attachOpen,
  });

  const { data: escalationLogs = [] } = useQuery({
    queryKey: ["alerts", id, "escalation-logs"],
    queryFn: () => alertsApi.getEscalationLogs(id!),
    enabled: !!id,
  });

  const acknowledgeMutation = useMutation({
    mutationFn: () => alertsApi.acknowledge(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ALERT(id ?? "") });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ALERTS });
    },
    onError: (err) => toast.error(apiErrorMessage(err, "Failed to acknowledge alert.")),
  });

  const linkMutation = useMutation({
    mutationFn: (incidentId?: number) => alertsApi.linkToIncident(id!, incidentId),
    onSuccess: (updated) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ALERT(id ?? "") });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ALERTS });
      setAttachOpen(false);
      setSelectedIncidentId("");
      if (updated.incidentId != null) navigate(ROUTES.INCIDENTS.DETAIL(updated.incidentId));
    },
    onError: (err) => toast.error(apiErrorMessage(err, "Failed to link alert to incident.")),
  });

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Loading…</div>;
  }

  if (!alert) {
    return <div className="text-sm text-destructive">Alert not found.</div>;
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Alerts", onClick: () => navigate(ROUTES.ALERTS.LIST) },
          { label: `#${alert.id}` },
        ]}
        actions={
          <>
            {!alert.resolvedAt && <StatusPill status={alert.impactAtFireTime} />}
            {alert.resolvedAt ? (
              <span className="rounded-lg border px-3 py-2 text-xs font-medium text-muted-foreground">
                Resolved
              </span>
            ) : (
              <span className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs font-medium text-red-600 dark:text-red-400">
                Active
              </span>
            )}
            {alert.acknowledgedAt ? (
              <div className="flex items-center gap-1.5 rounded-lg bg-green-500/10 border border-green-500/30 px-3 py-2 text-xs text-green-600 dark:text-green-400">
                <CheckCheck size={13} />
                <span>Acked by <strong>{alert.acknowledgedBy}</strong></span>
              </div>
            ) : !alert.resolvedAt && (
              <Button
                variant="outline"
                onClick={() => acknowledgeMutation.mutate()}
                disabled={acknowledgeMutation.isPending}
              >
                <CheckCheck size={13} />
                {acknowledgeMutation.isPending ? "…" : "Acknowledge"}
              </Button>
            )}
            <ActionButtons context="Alert" targetId={alert.id} />
          </>
        }
      />

      <SectionAccordion
        title="Overview"
        description="Where and when this alert fired"
        icon={<Info size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <div className="flex flex-col gap-5">
          {alert.source !== "Internal" ? (
            <Field label="Source">
              <div className="flex items-center gap-3">
                <AlertSourceBadge source={alert.source} sourceLabel={alert.sourceLabel} sourceIconifyIcon={alert.sourceIconifyIcon} />
                {alert.sourceUrl && (
                  <a
                    href={alert.sourceUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
                  >
                    View in {alert.sourceLabel ?? "source"} <ExternalLink size={12} />
                  </a>
                )}
              </div>
            </Field>
          ) : (
            <div className="grid grid-cols-2 gap-5">
              <Field label="Check">
                {alert.checkSlug && alert.serviceSlug ? (
                  <Link
                    to={ROUTES.CHECKS.DETAIL(alert.serviceSlug, alert.checkSlug)}
                    className="font-semibold hover:underline"
                  >
                    {alert.checkName}
                  </Link>
                ) : (
                  <span className="text-muted-foreground">—</span>
                )}
              </Field>
              <Field label="Service">
                {alert.serviceSlug ? (
                  <Link
                    to={ROUTES.SERVICES.DETAIL(alert.serviceSlug)}
                    className="font-semibold hover:underline"
                  >
                    {alert.serviceName}
                  </Link>
                ) : (
                  <span className="text-muted-foreground">—</span>
                )}
              </Field>
            </div>
          )}

          <div className="grid grid-cols-2 gap-5">
            <Field label="Fired At">{formatDate(alert.firedAt)}</Field>
            <Field label="Resolved At">
              {alert.resolvedAt ? formatDate(alert.resolvedAt) : <span className="text-red-600 font-medium">Active</span>}
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-5">
            <Field label="Occurrences">{alert.occurrenceCount}</Field>
            <Field label="Severity">{alert.severity ?? "—"}</Field>
          </div>

          <div className="flex flex-col gap-1">
            <span className="text-xs font-medium text-muted-foreground">Message</span>
            <p className="text-sm rounded-lg border bg-muted/30 px-3 py-2 font-mono">
              {alert.message ?? "—"}
            </p>
          </div>

          {alert.sourceRawPayload && (
            <div>
              <Button type="button" variant="outline" size="sm" onClick={() => setPayloadOpen(true)}>
                <FileJson size={13} />
                View raw payload
              </Button>
            </div>
          )}
        </div>
      </SectionAccordion>

      <SectionAccordion
        title="Trigger Criteria"
        description={alert.source !== "Internal" ? "Managed externally of Piro" : "The AlertConfig rule that fired this alert"}
        icon={<ListChecks size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        {alert.source !== "Internal" ? (
          <p className="text-sm text-muted-foreground">
            This alert was triggered by an external source — Piro doesn't evaluate its own criteria for it.
          </p>
        ) : (
          <div className="flex flex-col gap-4">
            <div className="grid grid-cols-2 gap-5">
              <Field label="Alert For">{alert.alertFor}</Field>
              <Field label="Value">{alert.alertValue}</Field>
              <Field label="Failure threshold">{alert.failureThreshold}</Field>
              <Field label="Success threshold">{alert.successThreshold}</Field>
            </div>
            {alert.alertConfigDescription && (
              <Field label="Description">{alert.alertConfigDescription}</Field>
            )}
          </div>
        )}
      </SectionAccordion>

      {alert.escalationExhaustedAt && !alert.resolvedAt && !alert.acknowledgedAt && (
        <EscalationHaltedBanner exhaustedAt={alert.escalationExhaustedAt} />
      )}

      <SectionAccordion
        title="Escalation"
        description="On-call notification attempts for this alert"
        icon={<History size={16} className="text-muted-foreground" />}
        defaultOpen={escalationLogs.length > 0}
        disableCard
      >
        <div className="rounded-xl border bg-card overflow-hidden">
          {escalationLogs.length === 0 ? (
            <div className="px-5 py-6 text-sm text-muted-foreground text-center">
              No escalation activity yet.
            </div>
          ) : (
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Step</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">User</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Channel</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Result</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">When</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {escalationLogs.map((log, i) => (
                  <tr key={i}>
                    <td className="px-5 py-3">{log.stepIndex + 1}</td>
                    <td className="px-5 py-3 font-medium">{log.userName}</td>
                    <td className="px-5 py-3 text-muted-foreground">{log.channelType}</td>
                    <td className="px-5 py-3">
                      {log.succeeded ? (
                        <span className="inline-flex items-center gap-1.5 text-green-600 dark:text-green-400">
                          <CheckCheck size={14} /> Sent
                        </span>
                      ) : (
                        <span
                          className="inline-flex items-center gap-1.5 text-destructive"
                          title={log.errorMessage ?? undefined}
                        >
                          <XCircle size={14} /> Failed
                        </span>
                      )}
                    </td>
                    <td className="px-5 py-3 text-muted-foreground">{formatDate(log.attemptedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </SectionAccordion>

      <SectionAccordion
        title="Incident"
        description="Manually create or attach this alert to an incident"
        icon={<Siren size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        {alert.incidentId != null ? (
          <div className="flex items-center justify-between gap-4">
            <div>
              <p className="text-sm font-semibold">Linked Incident</p>
              <p className="text-xs text-muted-foreground mt-0.5">{alert.incidentTitle ?? `Incident #${alert.incidentId}`}</p>
            </div>
            <Button variant="outline" onClick={() => navigate(ROUTES.INCIDENTS.DETAIL(alert.incidentId!))}>
              <ExternalLink size={14} />
              View incident
            </Button>
          </div>
        ) : (
          <div className="flex items-center justify-between gap-4">
            <div>
              <p className="text-sm font-semibold">Not linked</p>
              <p className="text-xs text-muted-foreground mt-0.5">This alert isn't linked to any incident yet.</p>
            </div>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                onClick={() => setAttachOpen(true)}
              >
                <Link2 size={14} />
                Attach to incident
              </Button>
              <Button
                onClick={() => linkMutation.mutate(undefined)}
                disabled={linkMutation.isPending}
              >
                <PlusCircle size={14} />
                {linkMutation.isPending ? "Creating…" : "Create incident"}
              </Button>
            </div>
          </div>
        )}
      </SectionAccordion>

      <Dialog open={attachOpen} onOpenChange={(open) => { if (!open) setAttachOpen(false); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach to incident</DialogTitle>
            <DialogDescription>Select an open incident to attach this alert's service to.</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            {openIncidents.length === 0 ? (
              <p className="text-sm text-muted-foreground">No open incidents.</p>
            ) : (
              <Select value={selectedIncidentId} onValueChange={(v) => setSelectedIncidentId(v ?? "")}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Select incident…" />
                </SelectTrigger>
                <SelectContent>
                  {openIncidents.map((inc) => (
                    <SelectItem key={inc.id} value={String(inc.id)}>
                      #{inc.id} — {inc.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAttachOpen(false)}>Cancel</Button>
            <Button
              disabled={!selectedIncidentId || linkMutation.isPending}
              onClick={() => linkMutation.mutate(Number(selectedIncidentId))}
            >
              {linkMutation.isPending ? "Attaching…" : "Attach"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {alert.sourceRawPayload && (
        <PayloadDialog
          open={payloadOpen}
          onOpenChange={setPayloadOpen}
          title="Webhook payload"
          description="The exact request that created this alert, unmodified."
          payload={alert.sourceRawPayload}
        />
      )}
    </>
  );
}
