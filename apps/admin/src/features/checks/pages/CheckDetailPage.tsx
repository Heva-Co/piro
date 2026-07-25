import { useParams, useNavigate } from "react-router-dom";
import { Bell, Play, Settings, AlertTriangle, ClipboardList, Clock, Wrench, Tags as TagsIcon, Filter } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import {
  useCheck,
  useDeleteCheck,
  useRunCheck,
  useAlertConfigs,
  useCheckTypeMeta,
} from "@/hooks/useChecks";
import { ROUTES } from "@/constants/routes";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { AlertConfigsSection } from "@/features/checks/components/detail/AlertConfigsSection";
import CheckTagsSection from "@/features/checks/components/detail/CheckTagsSection";
import CheckRequiredWorkerTagsSection from "@/features/checks/components/detail/CheckRequiredWorkerTagsSection";
import GeneralSettingsSection from "@/features/checks/components/detail/GeneralSettingsSection";
import ConfigurationSection from "@/features/checks/components/detail/ConfigurationSection";
import StatusHistorySection from "../components/detail/StatusHistorySection";
import RecentLogsSection from "../components/detail/RecentLogsSection";
import DangerZone from "@/components/DangerZone";
import { StatusPill } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import RecentLogsActions from "../components/detail/RecentLogsActions";

function CheckDetailPage() {
  const { slug: serviceSlug, checkSlug } = useParams<{ slug: string; checkSlug: string }>();
  const navigate = useNavigate();
  const { data: check, isLoading } = useCheck(serviceSlug!, checkSlug!);
  const runCheck = useRunCheck(serviceSlug!, checkSlug!);
  const deleteCheck = useDeleteCheck(serviceSlug!, checkSlug!);
  const typeMeta = useCheckTypeMeta(check?.type);
  const typeLabel = typeMeta?.displayName ?? check?.type ?? "";

  // For the section header warning: a check with no alert configs runs but notifies no one.
  const { data: alertConfigs } = useAlertConfigs(serviceSlug!, checkSlug!);
  const hasNoAlerts = alertConfigs !== undefined && alertConfigs.length === 0;

  async function handleDelete() {
    await deleteCheck.mutateAsync();
    navigate(ROUTES.SERVICES.DETAIL(serviceSlug!));
  }

  if (isLoading) {
    return (
      <PageContainer>
        <div className="text-sm text-muted-foreground">Loading…</div>
      </PageContainer>
    );
  }

  if (!check) {
    return (
      <PageContainer>
        <div className="text-sm text-destructive">Check not found.</div>
      </PageContainer>
    );
  }

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[
          { label: "Services", onClick: () => navigate(ROUTES.SERVICES.LIST) },
          { label: serviceSlug!, onClick: () => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!)) },
          { label: check.name },
        ]}
        actions={
          <>
            <span className="rounded-lg border px-3 py-1.5 text-sm text-muted-foreground">{typeLabel}</span>
            <StatusPill status={check.currentStatus} />
            <Button
              onClick={() => runCheck.mutate()}
              disabled={runCheck.isPending}
              variant="outline"
            >
              <Play size={12} />
              {runCheck.isPending ? "Running…" : "Run now"}
            </Button>
          </>
        }
      />

      <SectionAccordion
        title="General Settings"
        description="Basic information about this check"
        icon={<Settings size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <GeneralSettingsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title="Configuration"
        description={`Settings for the ${typeLabel} check`}
        icon={<Wrench size={16} className="text-muted-foreground" />}
      >
        <ConfigurationSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title="Tags"
        description="Organize this check and its service with key/value tags"
        icon={<TagsIcon size={16} className="text-muted-foreground" />}
      >
        <CheckTagsSection checkId={check.id} />
      </SectionAccordion>

      {!typeMeta?.singleRegionOnly && (
        <SectionAccordion
          title="Required worker tags"
          description="Restrict which workers may run this check"
          icon={<Filter size={16} className="text-muted-foreground" />}
        >
          <CheckRequiredWorkerTagsSection checkId={check.id} />
        </SectionAccordion>
      )}

      <SectionAccordion
        title={
          hasNoAlerts ? (
            <span className="flex items-center gap-2">
              Alert Configurations
              <span className="flex items-center gap-1 rounded-full bg-amber-500/15 px-2 py-0.5 text-xs font-medium text-amber-600 dark:text-amber-400">
                <AlertTriangle size={12} />
                No alerts
              </span>
            </span>
          ) : (
            "Alert Configurations"
          )
        }
        description="Notification channels triggered by this check"
        icon={<Bell size={16} className="text-muted-foreground" />}
        disableCard
      >
        <AlertConfigsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} dimensions={typeMeta?.dimensions ?? []} />
      </SectionAccordion>

      <SectionAccordion
        title="Recent Logs"
        description="Latest check executions"
        icon={<ClipboardList size={16} className="text-muted-foreground" />}
        actions={<RecentLogsActions serviceSlug={serviceSlug!} checkSlug={checkSlug!} />}
        disableCard
      >
        <RecentLogsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} dimensions={typeMeta?.dimensions ?? []} />
      </SectionAccordion>

      <SectionAccordion
        title="Status History"
        description="Uptime and status over the last 14 days. Scheduling gaps (monitor outage, unschedulable) never ran on a worker and are not shown here."
        icon={<Clock size={16} className="text-muted-foreground" />}
      >
        <StatusHistorySection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </SectionAccordion>

      <SectionAccordion
        title="Danger Zone"
        description="Irreversible actions for this check"
        icon={<AlertTriangle size={16} className="text-destructive" />}
        titleClassName="text-destructive"
        disableCard
      >
        <DangerZone objectName="check" objectId={checkSlug!} onDelete={handleDelete} />
      </SectionAccordion>
    </PageContainer>
  );
}

export default CheckDetailPage;
