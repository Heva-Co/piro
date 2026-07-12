import { notFound } from "next/navigation";
import { publicApi } from "@/src/lib/api";
import { ServiceStatusCard } from "@/src/components/ServiceStatusCard";
import { ServiceDetailTabsShell } from "@/src/components/ServiceDetailTabsShell";
import { IncidentCard } from "@/src/components/IncidentCard";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ days?: string }>;
}

export default async function IncidentsTabPage({ params, searchParams }: Props) {
  const { slug } = await params;
  const { days } = await searchParams;

  let service;
  try {
    service = await publicApi.service(slug);
  } catch {
    notFound();
  }

  const selectedDays = days ? Number(days) : service.historyDaysDesktop;
  const [overview, incidents] = await Promise.all([
    publicApi.overview(slug, selectedDays),
    publicApi.incidents(true).catch(() => []),
  ]);

  const serviceIncidents = incidents.filter((i) =>
    i.services.some((s) => s.serviceSlug === slug)
  );
  const activeIncidentsCount = serviceIncidents.filter((i) => i.status !== "Resolved").length;
  // Note: `incidents(true)` includes resolved incidents so the tab list can show history,
  // while the tab badge above still counts only non-resolved ones.

  return (
    <div className="flex flex-col gap-4">
      <ServiceStatusCard overview={overview} />

      <ServiceDetailTabsShell
        slug={slug}
        historyDaysDesktop={service.historyDaysDesktop}
        incidentsCount={activeIncidentsCount}
      >
        <div className="p-5 flex flex-col gap-3">
          {serviceIncidents.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-8">No incidents recorded</p>
          ) : (
            serviceIncidents.map((incident) => <IncidentCard key={incident.id} incident={incident} />)
          )}
        </div>
      </ServiceDetailTabsShell>
    </div>
  );
}
