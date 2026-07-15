import { Suspense } from "react";
import { notFound } from "next/navigation";
import { resolveHistoryDays } from "@/src/lib/utils";
import { servicesApi } from "@/src/lib/actions/services";
import { incidentsApi } from "@/src/lib/actions/incidents";
import { ServiceStatusCard } from "@/src/components/ServiceStatusCard";
import { ServiceStatusCardSkeleton } from "@/src/components/ServiceStatusCardSkeleton";
import { ServiceDetailTabsShell } from "@/src/components/ServiceDetailTabsShell";
import { ServiceTabsNav } from "@/src/components/ServiceTabsNav";
import { ServiceTabsNavSkeleton } from "@/src/components/ServiceTabsNavSkeleton";
import { IncidentCard } from "@/src/components/IncidentCard";
import { IncidentsTabContentSkeleton } from "@/src/components/IncidentsTabContentSkeleton";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ days?: string }>;
}

export default async function IncidentsTabPage({ params, searchParams }: Props) {
  const { slug } = await params;
  const { days } = await searchParams;

  try {
    await servicesApi.get(slug);
  } catch {
    notFound();
  }

  const selectedDays = resolveHistoryDays(days);

  return (
    <div className="flex flex-col gap-4">
      <Suspense fallback={<ServiceStatusCardSkeleton />}>
        <StatusCardSection slug={slug} days={selectedDays} />
      </Suspense>

      <ServiceDetailTabsShell
        nav={
          <Suspense fallback={<ServiceTabsNavSkeleton />}>
            <TabsNavSection slug={slug} defaultDays={selectedDays} />
          </Suspense>
        }
      >
        <Suspense fallback={<IncidentsTabContentSkeleton />}>
          <IncidentsTabContentSection slug={slug} />
        </Suspense>
      </ServiceDetailTabsShell>
    </div>
  );
}

async function StatusCardSection(props: { slug: string; days: number }) {
  const overview = await servicesApi.overview(props.slug, props.days);
  return <ServiceStatusCard overview={overview} />;
}

async function TabsNavSection(props: { slug: string; defaultDays: number }) {
  // Note: the tab badge counts only non-resolved incidents, independent of the tab
  // content below (which includes resolved incidents for history).
  const incidents = await incidentsApi.list(false).catch(() => []);
  const activeIncidentsCount = incidents.filter((i) =>
    i.services.some((s) => s.serviceSlug === props.slug)
  ).length;
  return (
    <ServiceTabsNav slug={props.slug} defaultDays={props.defaultDays} incidentsCount={activeIncidentsCount} />
  );
}

async function IncidentsTabContentSection(props: { slug: string }) {
  const incidents = await incidentsApi.list(true).catch(() => []);
  const serviceIncidents = incidents.filter((i) =>
    i.services.some((s) => s.serviceSlug === props.slug)
  );

  return (
    <div className="p-5 flex flex-col gap-3">
      {serviceIncidents.length === 0 ? (
        <p className="text-sm text-muted-foreground text-center py-8">No incidents recorded</p>
      ) : (
        serviceIncidents.map((incident) => <IncidentCard key={incident.id} incident={incident} />)
      )}
    </div>
  );
}
