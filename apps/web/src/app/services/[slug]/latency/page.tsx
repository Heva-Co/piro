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
import { LatencyTabContent } from "@/src/components/LatencyTabContent";
import { LatencyTabContentSkeleton } from "@/src/components/LatencyTabContentSkeleton";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ days?: string }>;
}

export default async function LatencyTabPage({ params, searchParams }: Props) {
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
        <Suspense fallback={<LatencyTabContentSkeleton />}>
          <LatencyTabContentSection slug={slug} days={selectedDays} />
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

async function LatencyTabContentSection(props: { slug: string; days: number }) {
  const overview = await servicesApi.overview(props.slug, props.days);
  return (
    <div className="p-5 flex flex-col gap-5">
      <LatencyTabContent
        dailyData={overview.dailyData}
        overallAvgLatencyMs={overview.overallAvgLatencyMs}
        overallMinLatencyMs={overview.overallMinLatencyMs}
        overallMaxLatencyMs={overview.overallMaxLatencyMs}
      />
    </div>
  );
}
