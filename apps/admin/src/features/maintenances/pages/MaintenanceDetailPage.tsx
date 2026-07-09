import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, CalendarClock, Settings } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { PageHeader } from "@/components/PageHeader";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone";
import { maintenancesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import MaintenanceGeneralSettingsSection from "../components/MaintenanceGeneralSettingsSection";
import MaintenanceEventsSection from "../components/MaintenanceEventsSection";

const DISPLAY_STATUS_BADGE: Record<string, string> = {
  Active: "bg-green-500/15 text-green-600 dark:text-green-400",
  Scheduled: "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  Completed: "bg-indigo-100 text-indigo-700",
  Cancelled: "bg-muted text-muted-foreground",
};

function isOneTime(rRule: string) {
  return rRule.includes("COUNT=1");
}

export default function MaintenanceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const maintenanceKey = QUERY_KEYS.MAINTENANCE(id!);

  const { data: maintenance, isLoading } = useQuery({
    queryKey: maintenanceKey,
    queryFn: () => maintenancesApi.get(id!),
  });

  async function handleCancel() {
    await maintenancesApi.cancel(id!);
    qc.invalidateQueries({ queryKey: maintenanceKey });
    qc.invalidateQueries({ queryKey: QUERY_KEYS.MAINTENANCES });
  }

  async function handleDelete() {
    await maintenancesApi.delete(id!);
    navigate(ROUTES.MAINTENANCES.LIST);
  }

  if (isLoading) {
    return (
      <AdminLayout title="Maintenance">
        <div className="text-sm text-muted-foreground">Loading…</div>
      </AdminLayout>
    );
  }

  if (!maintenance) {
    return (
      <AdminLayout title="Maintenance">
        <div className="text-sm text-destructive">Maintenance not found.</div>
      </AdminLayout>
    );
  }

  const oneTime = isOneTime(maintenance.rRule);
  const isCancelled = maintenance.displayStatus === "Cancelled";

  return (
    <AdminLayout title={maintenance.title}>
      <PageHeader
        breadcrumbs={[
          { label: "Maintenances", onClick: () => navigate(ROUTES.MAINTENANCES.LIST) },
          { label: maintenance.title },
        ]}
        actions={
          <span className={`inline-flex items-center rounded-lg px-3 py-1.5 border text-sm font-semibold ${DISPLAY_STATUS_BADGE[maintenance.displayStatus] ?? "bg-muted text-muted-foreground"}`}>
            {maintenance.displayStatus}
          </span>
        }
      />

      <SectionAccordion
        title="General Settings"
        description="Title, schedule and duration for this maintenance"
        icon={<Settings size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <MaintenanceGeneralSettingsSection maintenance={maintenance} />
      </SectionAccordion>

      {!oneTime && (
        <SectionAccordion
          title={`Upcoming Events (${maintenance.upcomingEvents.length})`}
          description="Individual occurrences of this recurring maintenance"
          icon={<CalendarClock size={16} className="text-muted-foreground" />}
        >
          <MaintenanceEventsSection maintenance={maintenance} />
        </SectionAccordion>
      )}

      <SectionAccordion
        title="Danger Zone"
        description="Irreversible actions for this maintenance"
        icon={<AlertTriangle size={16} className="text-destructive" />}
        titleClassName="text-destructive"
      >
        <div className="flex flex-col gap-4">
          {oneTime && !isCancelled && (
            <DangerZone
              objectName="maintenance"
              objectId={maintenance.title}
              onDelete={handleCancel}
              variant="cancel"
            />
          )}
          <DangerZone
            objectName="maintenance"
            objectId={maintenance.title}
            onDelete={handleDelete}
            variant="delete"
          />
        </div>
      </SectionAccordion>
    </AdminLayout>
  );
}
