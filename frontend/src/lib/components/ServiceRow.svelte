<script lang="ts">
  import type { PublicService, ServiceOverviewDto } from "$lib/api";
  import { formatLatency } from "$lib/utils";
  import StatusBarCalendar from "./StatusBarCalendar.svelte";
  import CircleCheck from "lucide-svelte/icons/circle-check";
  import CircleX from "lucide-svelte/icons/circle-x";
  import TriangleAlert from "lucide-svelte/icons/triangle-alert";
  import Wrench from "lucide-svelte/icons/wrench";
  import CircleMinus from "lucide-svelte/icons/circle-minus";

  let { service, overview }: { service: PublicService; overview: ServiceOverviewDto | null } = $props();

  const statusIcon = {
    UP:          CircleCheck,
    DEGRADED:    TriangleAlert,
    DOWN:        CircleX,
    MAINTENANCE: Wrench,
    NO_DATA:     CircleMinus,
  } as const;

  const statusColor: Record<string, string> = {
    UP:          "text-green-500",
    DEGRADED:    "text-amber-500",
    DOWN:        "text-red-500",
    MAINTENANCE: "text-indigo-500",
    NO_DATA:     "text-muted-foreground",
  };

  const fromDate = $derived(
    overview
      ? new Date(overview.fromTimestamp * 1000).toLocaleDateString(undefined, { month: "short", day: "numeric" })
      : null
  );

  const toDate = $derived(
    overview
      ? new Date(overview.toTimestamp * 1000).toLocaleDateString(undefined, { month: "short", day: "numeric" })
      : null
  );
</script>

<a href="/services/{service.slug}" class="flex flex-col gap-3 px-4 py-4 sm:px-5 hover:bg-muted/40 transition-colors group">
  <!-- Top row: icon + name + uptime + status label -->
  <div class="flex items-center gap-3">
    {#if service.imageUrl}
      <img src={service.imageUrl} alt={service.name} class="size-9 rounded-lg object-cover shrink-0 hidden sm:block" />
    {/if}

    <div class="flex-1 min-w-0">
      <p class="font-medium text-sm truncate">{service.name}</p>
      {#if service.description}
        <p class="text-xs text-muted-foreground truncate mt-0.5">{service.description}</p>
      {/if}
    </div>

    <div class="flex items-center gap-4 shrink-0">
      {#if overview}
        <div class="hidden sm:flex flex-col items-end gap-0.5">
          <span class="text-sm font-semibold">{overview.uptimePercent.toFixed(2)}%</span>
          {#if overview.overallAvgLatencyMs}
            <span class="text-xs text-muted-foreground">{formatLatency(overview.overallAvgLatencyMs)}</span>
          {/if}
        </div>
      {/if}
      <svelte:component this={statusIcon[service.status] ?? statusIcon.NO_DATA} class="size-5 {statusColor[service.status]}" />
    </div>
  </div>

  <!-- Status bar -->
  {#if overview && overview.dailyData.length > 0}
    <div class="flex flex-col gap-1">
      <StatusBarCalendar data={overview.dailyData} barHeight={40} radius={8} />
      <div class="flex justify-between text-xs text-muted-foreground">
        <span>{fromDate}</span>
        <span>{toDate}</span>
      </div>
    </div>
  {/if}
</a>
