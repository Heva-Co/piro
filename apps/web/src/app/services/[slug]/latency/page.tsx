import { notFound } from "next/navigation";
import { publicApi } from "@/src/lib/api";
import { ServiceStatusCard } from "@/src/components/ServiceStatusCard";
import { ServiceDetailTabsShell } from "@/src/components/ServiceDetailTabsShell";
import { LatencyTabContent } from "@/src/components/LatencyTabContent";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ days?: string }>;
}

export default async function LatencyTabPage({ params, searchParams }: Props) {
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
    publicApi.incidents(false).catch(() => []),
  ]);

  const activeIncidentsCount = incidents.filter((i) =>
    i.services.some((s) => s.serviceSlug === slug)
  ).length;

  return (
    <div className="flex flex-col gap-4">
      <ServiceStatusCard overview={overview} />

      <ServiceDetailTabsShell
        slug={slug}
        historyDaysDesktop={service.historyDaysDesktop}
        incidentsCount={activeIncidentsCount}
      >
        <div className="p-5 flex flex-col gap-5">
          <LatencyTabContent
            dailyData={overview.dailyData}
            overallAvgLatencyMs={overview.overallAvgLatencyMs}
            overallMinLatencyMs={overview.overallMinLatencyMs}
            overallMaxLatencyMs={overview.overallMaxLatencyMs}
          />
        </div>
      </ServiceDetailTabsShell>
    </div>
  );
}
