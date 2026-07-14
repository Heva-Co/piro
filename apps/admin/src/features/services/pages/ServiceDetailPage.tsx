import { useParams, useNavigate } from "react-router-dom";
import { AlertTriangle, ClipboardList, Settings } from "lucide-react";
import { StatusPill } from "@/components/StatusBadge";
import { useService, useDeleteService } from "@/hooks/useServices";
import { useChecks } from "@/hooks/useChecks";
import { ROUTES } from "@/constants/routes";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import ChecksSection from "../components/ChecksSection";
import GeneralSettingsSection from "../components/GeneralSettingsSection";

export default function ServiceDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const { data: service, isLoading } = useService(slug!);
  const { data: checks } = useChecks(slug!);
  const deleteService = useDeleteService(slug!);

  async function handleDeleteService() {
    await deleteService.mutateAsync();
    navigate(ROUTES.SERVICES.LIST);
  }

  if (isLoading) {
    return (
      <>
        <div className="text-sm text-muted-foreground">Loading…</div>
      </>
    );
  }

  if (!service) {
    return (
      <>
        <div className="text-sm text-destructive">Service not found.</div>
      </>
    );
  }

  return (
    <>
      <div>
        <PageHeader
          breadcrumbs={[
            { label: "Services", onClick: () => navigate(ROUTES.SERVICES.LIST) },
            { label: service.name },
          ]}
          actions={
            <>
              <StatusPill status={service.currentStatus}/>
            </>
          }
        />

        {/* Accordion sections */}
        <SectionAccordion
          title="General Settings"
          description="Basic information about this service"
          icon={<Settings size={16} className="text-muted-foreground" />}
          defaultOpen
        >
          <GeneralSettingsSection slug={slug!} />
        </SectionAccordion>

        <SectionAccordion
          title={`Checks (${checks?.length ?? 0})`}
          description="Checks configured for this service"
          icon={<ClipboardList size={16} className="text-muted-foreground" />}
          actions={<Button onClick={() => navigate(`/admin/services/${slug}/checks/new`)}> + Add Check</Button>}
          disableCard
        >
          <ChecksSection slug={slug!} />
        </SectionAccordion>

        <SectionAccordion
          title="Danger Zone"
          description="Irreversible actions for this service"
          icon={<AlertTriangle size={16} className="text-destructive" />}
          titleClassName="text-destructive"
          disableCard
        >
          <DangerZone objectName="service" objectId={slug!} onDelete={handleDeleteService} />
        </SectionAccordion>
      </div>
    </>
  );
}
