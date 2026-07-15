"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState, useTransition } from "react";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/src/components/ui/select";

type Tab = "status" | "latency" | "incidents" | "maintenances";

const DAY_OPTIONS = [
  { label: "7 Days", value: 7 },
  { label: "14 Days", value: 14 },
  { label: "30 Days", value: 30 },
];

interface Props {
  slug: string;
  defaultDays: number;
  incidentsCount: number;
}

export function ServiceTabsNav({ slug, defaultDays, incidentsCount }: Props) {
  const pathname = usePathname();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [isPending, startTransition] = useTransition();
  const [pendingDays, setPendingDays] = useState<number | null>(null);

  const daysParam = Number(searchParams.get("days"));
  const selectedDays = Number.isFinite(daysParam) && daysParam > 0 ? daysParam : defaultDays;

  // The day-range selector is hidden below the `sm` breakpoint (no room for it there), so the
  // server always renders with `defaultDays`. Once mounted, correct that to 7 days on an actual
  // mobile viewport — but only when the URL has no explicit ?days=, so we never override a value
  // the user (or a shared link) already picked.
  useEffect(() => {
    if (searchParams.get("days")) return;
    if (defaultDays === 7) return;
    if (window.matchMedia("(max-width: 639px)").matches) {
      const params = new URLSearchParams(searchParams.toString());
      params.set("days", "7");
      router.replace(`${pathname}?${params.toString()}`);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
      <div className="flex min-w-0 bg-transparent p-0 h-auto rounded-none gap-0 overflow-x-auto">
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

      <Select
        items={DAY_OPTIONS.map((opt) => ({ label: opt.label, value: String(opt.value) }))}
        value={String(displayedDays)}
        onValueChange={(value) => changeDays(Number(value))}
      >
        <SelectTrigger
          size="sm"
          className="hidden sm:flex rounded-full text-xs mb-2 shrink-0"
        >
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectGroup>
            {DAY_OPTIONS.map((opt) => (
              <SelectItem key={opt.value} value={String(opt.value)}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectGroup>
        </SelectContent>
      </Select>
    </div>
  );
}
