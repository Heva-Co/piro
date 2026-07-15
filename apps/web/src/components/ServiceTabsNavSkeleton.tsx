/** Non-interactive placeholder matching ServiceTabsNav's exact layout, shown while incidentsCount resolves so the tabs/selector never appear to "reload". */
export function ServiceTabsNavSkeleton() {
  return (
    <div className="flex items-center justify-between px-5 pt-4 pb-0 gap-4 border-b">
      <div className="flex min-w-0 gap-0 overflow-x-auto">
        {["Status", "Latency", "Incidents", "Maintenances"].map((label) => (
          <span key={label} className="px-4 pb-3 pt-1 text-sm font-medium text-muted-foreground">
            {label}
          </span>
        ))}
      </div>
      <span className="hidden sm:flex h-7 w-20 mb-2 shrink-0 rounded-full border" />
    </div>
  );
}
