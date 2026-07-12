import { notFound } from "next/navigation";
import { publicApi } from "@/src/lib/api";
import { ServiceStatusCard } from "@/src/components/ServiceStatusCard";
import { ServiceDetailTabsShell } from "@/src/components/ServiceDetailTabsShell";
import { MaintenanceCard } from "@/src/components/MaintenanceCard";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ days?: string }>;
}

export default async function MaintenancesTabPage({ params, searchParams }: Props) {
  const { slug } = await params;
  const { days } = await searchParams;

  let service;
  try {
    service = await publicApi.service(slug);
  } catch {
    notFound();
  }

  const selectedDays = days ? Number(days) : service.historyDaysDesktop;
  const [overview, incidents, maintenances] = await Promise.all([
    publicApi.overview(slug, selectedDays),
    publicApi.incidents(false).catch(() => []),
    publicApi.maintenances().catch(() => []),
  ]);

  const activeIncidentsCount = incidents.filter((i) =>
    i.services.some((s) => s.serviceSlug === slug)
  ).length;
  const serviceMaintenances = maintenances.filter((m) => m.serviceSlugs.includes(slug));

  return (
    <div className="flex flex-col gap-4">
      <ServiceStatusCard overview={overview} />

      <ServiceDetailTabsShell
        slug={slug}
        historyDaysDesktop={service.historyDaysDesktop}
        incidentsCount={activeIncidentsCount}
      >
        <div className="p-5 flex flex-col gap-3">
          {serviceMaintenances.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-8">
              No maintenances scheduled
            </p>
          ) : (
            serviceMaintenances.map((m) => <MaintenanceCard key={m.id} maintenance={m} />)
          )}
        </div>
      </ServiceDetailTabsShell>
    </div>
  );
}
