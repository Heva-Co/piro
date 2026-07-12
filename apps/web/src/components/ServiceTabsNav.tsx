"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState, useTransition } from "react";

type Tab = "status" | "latency" | "incidents" | "maintenances";

const DAY_OPTIONS = [
  { label: "7 Days", value: 7 },
  { label: "14 Days", value: 14 },
  { label: "30 Days", value: 30 },
  { label: "60 Days", value: 60 },
  { label: "90 Days", value: 90 },
];

interface Props {
  slug: string;
  historyDaysDesktop: number;
  defaultDays: number;
  incidentsCount: number;
}

export function ServiceTabsNav({ slug, historyDaysDesktop, defaultDays, incidentsCount }: Props) {
  const pathname = usePathname();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [isPending, startTransition] = useTransition();
  const [pendingDays, setPendingDays] = useState<number | null>(null);

  const daysParam = Number(searchParams.get("days"));
  const selectedDays = Number.isFinite(daysParam) && daysParam > 0 ? daysParam : defaultDays;

  const availableDayOptions = DAY_OPTIONS.filter((o) => o.value <= historyDaysDesktop);

  const tabs: { id: Tab; label: string; badge?: number }[] = [
    { id: "status", label: "Status" },
    { id: "latency", label: "Latency" },
    { id: "incidents", label: "Incidents", badge: incidentsCount > 0 ? incidentsCount : undefined },
    { id: "maintenances", label: "Maintenances" },
  ];

  const activeTab = tabs.find((t) => pathname.endsWith(`/${t.id}`))?.id ?? "status";

  function hrefFor(id: Tab) {
    const params = new URLSearchParams(searchParams.toString());
    return `/services/${slug}/${id}${params.toString() ? `?${params.toString()}` : ""}`;
  }

  function changeDays(days: number) {
    setPendingDays(days);
    const params = new URLSearchParams(searchParams.toString());
    params.set("days", String(days));
    startTransition(() => {
      router.push(`${pathname}?${params.toString()}`);
    });
  }

  const displayedDays = pendingDays ?? selectedDays;

  return (
    <div
      className={`flex items-center justify-between px-5 pt-4 pb-0 gap-4 border-b transition-opacity ${
        isPending ? "opacity-50" : ""
      }`}
    >
      <div className="flex bg-transparent p-0 h-auto rounded-none gap-0">
        {tabs.map(({ id, label, badge }) => (
          <Link
            key={id}
            href={hrefFor(id)}
            className={`relative rounded-none bg-transparent px-4 pb-3 pt-1 text-sm font-medium shadow-none transition-colors ${
              activeTab === id
                ? "text-foreground after:scale-x-100"
                : "text-muted-foreground hover:text-foreground after:scale-x-0"
            } after:absolute after:bottom-0 after:left-0 after:right-0 after:h-0.5 after:rounded-full after:bg-foreground after:transition-transform`}
          >
            {label}
            {badge !== undefined && (
              <span className="ml-1.5 inline-flex size-4 items-center justify-center rounded-full bg-muted text-[10px] text-destructive font-semibold">
                {badge}
              </span>
            )}
          </Link>
        ))}
      </div>

      <select
        value={displayedDays}
        onChange={(e) => changeDays(Number(e.target.value))}
        className="text-xs rounded-full border px-3 h-8 bg-background mb-2 shrink-0"
      >
        {availableDayOptions.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
    </div>
  );
}
