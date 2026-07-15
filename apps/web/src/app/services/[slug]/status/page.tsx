import { Suspense } from "react";
import { notFound } from "next/navigation";
import { servicesApi } from "@/src/lib/actions/services";
import { incidentsApi } from "@/src/lib/actions/incidents";
import { ServiceStatusCard } from "@/src/components/ServiceStatusCard";
import { ServiceStatusCardSkeleton } from "@/src/components/ServiceStatusCardSkeleton";
import { DayDetailCalendar } from "@/src/components/DayDetailDialog";
import { formatUtcDateLong, resolveHistoryDays } from "@/src/lib/utils";
import { ServiceDetailTabsShell } from "@/src/components/ServiceDetailTabsShell";
import { ServiceTabsNav } from "@/src/components/ServiceTabsNav";
import { ServiceTabsNavSkeleton } from "@/src/components/ServiceTabsNavSkeleton";
import { StatusTabContentSkeleton } from "@/src/components/StatusTabContentSkeleton";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ days?: string }>;
}

export default async function StatusTabPage({ params, searchParams }: Props) {
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
        <Suspense fallback={<StatusTabContentSkeleton />}>
          <StatusTabContentSection slug={slug} days={selectedDays} />
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

async function StatusTabContentSection(props: { slug: string; days: number }) {
  const overview = await servicesApi.overview(props.slug, props.days);
  const fromDate = formatUtcDateLong(overview.fromTimestamp);
  const toDate = formatUtcDateLong(overview.toTimestamp);

  return (
    <div className="p-5 flex flex-col gap-5">
      <div className="flex flex-col gap-0.5">
        <p className="text-3xl font-bold">{overview.uptimePercent.toFixed(1)}%</p>
        <p className="text-xs text-muted-foreground">Uptime</p>
      </div>

      {overview.dailyData.length > 0 ? (
        <div className="flex flex-col gap-1">
          <DayDetailCalendar slug={props.slug} dailyData={overview.dailyData} />
          <div className="flex justify-between text-xs text-muted-foreground mt-1">
            <span>{fromDate}</span>
            <span>{toDate}</span>
          </div>
        </div>
      ) : (
        <div className="h-12 rounded-lg bg-muted/50 flex items-center justify-center text-xs text-muted-foreground">
          No history data yet
        </div>
      )}
    </div>
  );
}
