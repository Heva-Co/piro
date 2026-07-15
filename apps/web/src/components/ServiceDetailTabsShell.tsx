interface Props {
  /**
   * `ServiceTabsNav` (or, while `incidentsCount` is still resolving inside a `<Suspense>`,
   * `ServiceTabsNavSkeleton`) — passed as a slot rather than rendered internally so each page can
   * choose its own Suspense boundary around it without this shell needing to know about that.
   */
  nav: React.ReactNode;
  children: React.ReactNode;
}

/**
 * Bordered card wrapping the tabs nav + tab-specific content, shared by every
 * `/services/[slug]/*` subroute. Each page renders its own `ServiceStatusCard`
 * above this shell (outside the border), since the day-range affecting the
 * status card comes from `searchParams`, which layouts cannot read.
 */
export function ServiceDetailTabsShell({ nav, children }: Props) {
  return (
    <div className="bg-background rounded-3xl border">
      {nav}
      {children}
    </div>
  );
}
