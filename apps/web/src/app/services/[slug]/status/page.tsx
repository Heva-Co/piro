import { notFound } from "next/navigation";
import { publicApi } from "@/src/lib/api";
import { ServiceStatusCard } from "@/src/components/ServiceStatusCard";
import { DayDetailCalendar } from "@/src/components/DayDetailDialog";
import { formatUtcDateLong } from "@/src/lib/utils";
import { ServiceDetailTabsShell } from "@/src/components/ServiceDetailTabsShell";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ days?: string }>;
}

export default async function StatusTabPage({ params, searchParams }: Props) {
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

  const fromDate = formatUtcDateLong(overview.fromTimestamp);
  const toDate = formatUtcDateLong(overview.toTimestamp);

  return (
    <>
      <div className="mb-4">
        <ServiceStatusCard overview={overview} />
      </div>

      <ServiceDetailTabsShell
        slug={slug}
        historyDaysDesktop={service.historyDaysDesktop}
        incidentsCount={activeIncidentsCount}
      >
        <div className="p-5 flex flex-col gap-5">
          <div className="flex flex-col gap-0.5">
            <p className="text-3xl font-bold">{overview.uptimePercent.toFixed(1)}%</p>
            <p className="text-xs text-muted-foreground">Uptime</p>
          </div>

          {overview.dailyData.length > 0 ? (
            <div className="flex flex-col gap-1">
              <DayDetailCalendar slug={slug} dailyData={overview.dailyData} />
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
      </ServiceDetailTabsShell>
    </>
  );
}
