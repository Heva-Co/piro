import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Settings, ListOrdered } from "lucide-react";
import { escalationApi } from "@/lib/api";
import type { UpsertEscalationPolicyRequest } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { PageHeader } from "@/components/PageHeader";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone";
import GeneralSettingsSection from "../components/GeneralSettingsSection";
import EscalationStepsSection from "../components/EscalationStepsSection";

export default function EscalationPolicyDetailPage() {
  const { policyId } = useParams<{ policyId: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const { data: policy, isLoading } = useQuery({
    queryKey: QUERY_KEYS.ESCALATION_POLICY(policyId!),
    queryFn: () => escalationApi.get(policyId!),
    enabled: !!policyId,
  });

  const deleteMutation = useMutation({
    mutationFn: () => escalationApi.delete(policyId!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ESCALATION_POLICIES });
    },
  });

  async function handleDelete() {
    await deleteMutation.mutateAsync();
    navigate(ROUTES.ESCALATION.LIST);
  }

  function buildRequest(overrides?: Partial<UpsertEscalationPolicyRequest>): UpsertEscalationPolicyRequest {
    return {
      name: policy!.name,
      description: policy!.description,
      reEscalateAfterInactivityMinutes: policy!.reEscalateAfterInactivityMinutes,
      steps: policy!.steps.map((s) => ({ order: s.order, delayMinutes: s.delayMinutes, scheduleId: s.scheduleId })),
      ...overrides,
    };
  }

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Loading…</div>;
  }

  if (!policy) {
    return <div className="text-sm text-destructive">Escalation policy not found.</div>;
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Escalation Policies", onClick: () => navigate(ROUTES.ESCALATION.LIST) },
          { label: policy.name },
        ]}
        subheader="Auto-notify on-call users when an alert goes unacknowledged."
      />

      <SectionAccordion
        title="General Settings"
        description="Name and re-escalation timeouts"
        icon={<Settings size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <GeneralSettingsSection policy={policy} buildRequest={buildRequest} />
      </SectionAccordion>

      <SectionAccordion
        title="Escalation Steps"
        description="Who gets notified, and when"
        icon={<ListOrdered size={16} className="text-muted-foreground" />}
      >
        <EscalationStepsSection policy={policy} buildRequest={buildRequest} />
      </SectionAccordion>

      <SectionAccordion
        title="Danger Zone"
        description="Irreversible actions for this policy"
        icon={<AlertTriangle size={16} className="text-destructive" />}
        titleClassName="text-destructive"
      >
        <DangerZone objectName="escalation policy" objectId={policy.name} onDelete={handleDelete} />
      </SectionAccordion>
    </>
  );
}
