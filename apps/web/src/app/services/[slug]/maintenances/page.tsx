import { Suspense } from "react";
import { notFound } from "next/navigation";
import { resolveHistoryDays } from "@/src/lib/utils";
import { servicesApi } from "@/src/lib/actions/services";
import { incidentsApi } from "@/src/lib/actions/incidents";
import { maintenancesApi } from "@/src/lib/actions/maintenances";
import { ServiceStatusCard } from "@/src/components/ServiceStatusCard";
import { ServiceStatusCardSkeleton } from "@/src/components/ServiceStatusCardSkeleton";
import { ServiceDetailTabsShell } from "@/src/components/ServiceDetailTabsShell";
import { ServiceTabsNav } from "@/src/components/ServiceTabsNav";
import { ServiceTabsNavSkeleton } from "@/src/components/ServiceTabsNavSkeleton";
import { MaintenanceCard } from "@/src/components/MaintenanceCard";
import { MaintenancesTabContentSkeleton } from "@/src/components/MaintenancesTabContentSkeleton";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ days?: string }>;
}

export default async function MaintenancesTabPage({ params, searchParams }: Props) {
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
        <Suspense fallback={<MaintenancesTabContentSkeleton />}>
          <MaintenancesTabContentSection slug={slug} />
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
  const incidents = await incidentsApi.list(false).catch(() => []);
  const activeIncidentsCount = incidents.filter((i) =>
    i.services.some((s) => s.serviceSlug === props.slug)
  ).length;
  return (
    <ServiceTabsNav slug={props.slug} defaultDays={props.defaultDays} incidentsCount={activeIncidentsCount} />
  );
}

async function MaintenancesTabContentSection(props: { slug: string }) {
  const maintenances = await maintenancesApi.list().catch(() => []);
  const serviceMaintenances = maintenances.filter((m) => m.serviceSlugs.includes(props.slug));

  return (
    <div className="p-5 flex flex-col gap-3">
      {serviceMaintenances.length === 0 ? (
        <p className="text-sm text-muted-foreground text-center py-8">
          No maintenances scheduled
        </p>
      ) : (
        serviceMaintenances.map((m) => <MaintenanceCard key={m.id} maintenance={m} />)
      )}
    </div>
  );
}
