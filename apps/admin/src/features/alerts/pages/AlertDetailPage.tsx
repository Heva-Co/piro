import { useParams, useNavigate } from "react-router-dom";
import { ExternalLink } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { StatusPill } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { useAlert } from "@/hooks/useChecks";
import { ROUTES } from "@/constants/routes";

function formatDate(value?: string) {
  if (!value) return "—";
  return new Date(value).toLocaleString();
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
  const { data: alert, isLoading } = useAlert(id);

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
        actions={<StatusPill status={alert.impactAtFireTime} />}
      />

      <div className="max-w-3xl flex flex-col gap-6">
        <div className="rounded-xl border bg-card p-5 flex flex-col gap-5">
          <div className="grid grid-cols-2 gap-5">
            <Field label="Check">
              <span className="font-semibold">{alert.checkName}</span>
              <span className="ml-2 text-xs text-muted-foreground font-mono">{alert.checkSlug}</span>
            </Field>
            <Field label="Service">
              <span className="font-semibold">{alert.serviceName}</span>
              <span className="ml-2 text-xs text-muted-foreground font-mono">{alert.serviceSlug}</span>
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-5">
            <Field label="Fired At">{formatDate(alert.firedAt)}</Field>
            <Field label="Resolved At">
              {alert.resolvedAt ? formatDate(alert.resolvedAt) : <span className="text-red-600 font-medium">Active</span>}
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-5">
            <Field label="Occurrences">{alert.occurrenceCount}</Field>
            <Field label="Severity">{alert.severity}</Field>
          </div>

          <div className="flex flex-col gap-1">
            <span className="text-xs font-medium text-muted-foreground">Message</span>
            <p className="text-sm rounded-lg border bg-muted/30 px-3 py-2 font-mono">
              {alert.message ?? "—"}
            </p>
          </div>
        </div>

        {/* Trigger criteria */}
        <div className="rounded-xl border bg-card p-5 flex flex-col gap-4">
          <div>
            <p className="text-sm font-semibold">Trigger Criteria</p>
            <p className="text-xs text-muted-foreground mt-0.5">The AlertConfig rule that fired this alert</p>
          </div>
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

        {/* Linked incident */}
        {alert.incidentId != null && (
          <div className="rounded-xl border bg-card p-5 flex items-center justify-between gap-4">
            <div>
              <p className="text-sm font-semibold">Linked Incident</p>
              <p className="text-xs text-muted-foreground mt-0.5">{alert.incidentTitle ?? `Incident #${alert.incidentId}`}</p>
            </div>
            <Button variant="outline" onClick={() => navigate(ROUTES.INCIDENTS.DETAIL(alert.incidentId!))}>
              <ExternalLink size={14} />
              View incident
            </Button>
          </div>
        )}
      </div>
    </>
  );
}
