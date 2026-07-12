import { ServiceTabsNav } from "./ServiceTabsNav";

interface Props {
  slug: string;
  historyDaysDesktop: number;
  incidentsCount: number;
  children: React.ReactNode;
}

/**
 * Bordered card wrapping the tabs nav + tab-specific content, shared by every
 * `/services/[slug]/*` subroute. Each page renders its own `ServiceStatusCard`
 * above this shell (outside the border), since the day-range affecting the
 * status card comes from `searchParams`, which layouts cannot read.
 */
export function ServiceDetailTabsShell({ slug, historyDaysDesktop, incidentsCount, children }: Props) {
  return (
    <div className="bg-background rounded-3xl border">
      <ServiceTabsNav
        slug={slug}
        historyDaysDesktop={historyDaysDesktop}
        defaultDays={historyDaysDesktop}
        incidentsCount={incidentsCount}
      />
      {children}
    </div>
  );
}
