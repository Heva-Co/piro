import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { toast } from "sonner";
import { Info, ListChecks, Siren, History } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import ActionButtons from "@/components/integration-actions/ActionButtons";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { useAlert } from "@/hooks/useChecks";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { alertsApi } from "@/lib/actions/alerts";
import EscalationHaltedBanner from "../components/EscalationHaltedBanner";
import AlertStatusActions from "../components/AlertStatusActions";
import AlertOverviewSection from "../components/AlertOverviewSection";
import AlertTriggerCriteriaSection from "../components/AlertTriggerCriteriaSection";
import AlertIncidentSection from "../components/AlertIncidentSection";
import EscalationLogsTable from "../components/EscalationLogsTable";
import AttachIncidentDialog from "../components/AttachIncidentDialog";
import { PayloadDialog } from "@/components/PayloadDialog";

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function AlertDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const { data: alert, isLoading } = useAlert(id);

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
    return (
      <PageContainer>
        <div className="text-sm text-muted-foreground">Loading…</div>
      </PageContainer>
    );
  }

  if (!alert) {
    return (
      <PageContainer>
        <div className="text-sm text-destructive">Alert not found.</div>
      </PageContainer>
    );
  }

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[
          { label: "Alerts", onClick: () => navigate(ROUTES.ALERTS.LIST) },
          { label: `#${alert.id}` },
        ]}
        actions={
          <>
            <AlertStatusActions
              alert={alert}
              isAcknowledging={acknowledgeMutation.isPending}
              onAcknowledge={() => acknowledgeMutation.mutate()}
            />
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
        <AlertOverviewSection alert={alert} onViewPayload={() => setPayloadOpen(true)} />
      </SectionAccordion>

      <SectionAccordion
        title="Trigger Criteria"
        description={alert.source !== "Internal" ? "Managed externally of Piro" : "The AlertConfig rule that fired this alert"}
        icon={<ListChecks size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <AlertTriggerCriteriaSection alert={alert} />
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
          <EscalationLogsTable logs={escalationLogs} />
        </div>
      </SectionAccordion>

      <SectionAccordion
        title="Incident"
        description="Manually create or attach this alert to an incident"
        icon={<Siren size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <AlertIncidentSection
          alert={alert}
          isCreating={linkMutation.isPending}
          onViewIncident={(incidentId) => navigate(ROUTES.INCIDENTS.DETAIL(incidentId))}
          onAttach={() => setAttachOpen(true)}
          onCreate={() => linkMutation.mutate(undefined)}
        />
      </SectionAccordion>

      <AttachIncidentDialog
        open={attachOpen}
        onOpenChange={setAttachOpen}
        openIncidents={openIncidents}
        selectedIncidentId={selectedIncidentId}
        onSelect={setSelectedIncidentId}
        onAttach={() => linkMutation.mutate(Number(selectedIncidentId))}
        isPending={linkMutation.isPending}
      />

      {alert.sourceRawPayload && (
        <PayloadDialog
          open={payloadOpen}
          onOpenChange={setPayloadOpen}
          title="Webhook payload"
          description="The exact request that created this alert, unmodified."
          payload={alert.sourceRawPayload}
        />
      )}
    </PageContainer>
  );
}

export default AlertDetailPage;
