<script lang="ts">
  import type { PageData } from "./$types";
  import StatusBarCalendar from "$lib/components/StatusBarCalendar.svelte";
  import LatencyTrendChart from "$lib/components/LatencyTrendChart.svelte";
  import IncidentCard from "$lib/components/IncidentCard.svelte";
  import MaintenanceCard from "$lib/components/MaintenanceCard.svelte";
  import { publicApi, type ServiceOverviewDto, type DailyStatsDto, type StatusPoint } from "$lib/api.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Tabs from "$lib/components/ui/tabs/index.js";
  import * as Dialog from "$lib/components/ui/dialog/index.js";
  import PerMinuteStatusGrid from "$lib/components/PerMinuteStatusGrid.svelte";

  let { data }: { data: PageData } = $props();

  const ALL_DAY_OPTIONS = [
    { label: "7 Days",  value: 7 },
    { label: "14 Days", value: 14 },
    { label: "30 Days", value: 30 },
    { label: "60 Days", value: 60 },
    { label: "90 Days", value: 90 },
  ];

  const DAY_OPTIONS = ALL_DAY_OPTIONS.filter(o => o.value <= data.service.historyDaysDesktop);

  let selectedDays = $state(data.service.historyDaysDesktop);
  let overview = $state<ServiceOverviewDto>(data.overview);
  let loading = $state(false);

  async function changeDays(days: number) {
    selectedDays = days;
    loading = true;
    try {
      overview = await publicApi.getOverview(data.service.slug, days);
    } catch (e) { console.error("getOverview failed", e); }
    finally { loading = false; }
  }

  const statusLabel: Record<string, string> = {
    UP: "All Systems Operational",
    DEGRADED: "Partial Outage",
    DOWN: "Major Outage",
    MAINTENANCE: "Under Maintenance",
    NO_DATA: "No Status Data",
  };

  const statusClass: Record<string, string> = {
    UP: "text-green-600 dark:text-green-400",
    DEGRADED: "text-yellow-600 dark:text-yellow-400",
    DOWN: "text-red-600 dark:text-red-400",
    MAINTENANCE: "text-blue-600 dark:text-blue-400",
    NO_DATA: "text-muted-foreground",
  };

  function formatLatency(ms: number | null | undefined): string {
    if (!ms) return "";
    if (ms >= 1000) return `${(ms / 1000).toFixed(2)}s`;
    return `${Math.round(ms)}ms`;
  }

  function formatTs(ts: number): string {
    return new Date(ts * 1000).toLocaleString(undefined, {
      month: "short", day: "numeric", year: "numeric",
      hour: "numeric", minute: "2-digit"
    });
  }

  const fromDate = $derived(
    new Date(overview.fromTimestamp * 1000).toLocaleDateString(undefined, {
      month: "short", day: "numeric", year: "numeric"
    })
  );

  const toDate = $derived(
    new Date(overview.toTimestamp * 1000).toLocaleDateString(undefined, {
      month: "short", day: "numeric", year: "numeric"
    })
  );

  let latencyMetric = $state<"avg" | "min" | "max">("avg");

  // Day detail dialog
  let dayDetailOpen = $state(false);
  let dayDetailDay = $state<DailyStatsDto | null>(null);
  let dayDetailHistory = $state<StatusPoint[]>([]);
  let dayDetailLoading = $state(false);

  async function openDayDetail(day: DailyStatsDto) {
    dayDetailDay = day;
    dayDetailOpen = true;
    dayDetailHistory = [];
    dayDetailLoading = true;
    try {
      dayDetailHistory = await publicApi.getHistory(data.service.slug, day.timestamp, day.timestamp + 86399);
    } catch (e) { console.error("day detail fetch failed", e); }
    finally { dayDetailLoading = false; }
  }

  function formatDayHeader(ts: number): string {
    return new Date(ts * 1000).toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
  }
</script>

<svelte:head>
  <title>{data.service.name} — Status</title>
</svelte:head>

<main class="mx-auto max-w-3xl px-4 py-10 flex flex-col gap-4">
  <!-- Back -->
  <a href="/" class="text-sm text-muted-foreground hover:text-foreground transition-colors">← Back</a>

  <!-- Title -->
  <div class="flex flex-col gap-1 px-1">
    {#if data.service.imageUrl}
      <img src={data.service.imageUrl} alt={data.service.name} class="size-12 rounded-xl object-cover mb-1" />
    {/if}
    <h1 class="text-2xl font-bold">{data.service.name}</h1>
    {#if data.service.description}
      <p class="text-sm text-muted-foreground">{data.service.description}</p>
    {/if}
  </div>

  <!-- Status card -->
  <div class="bg-background rounded-3xl border p-5 flex flex-col gap-3">
    <div class="flex flex-col gap-0.5">
      <p class="text-sm font-medium">Last Updated</p>
      <p class="text-xs text-muted-foreground">{formatTs(overview.lastUpdatedAt)}</p>
    </div>
    <div class="flex items-end justify-between">
      <div class="flex flex-col gap-1">
        <p class="text-2xl font-semibold {statusClass[overview.currentStatus]}">
          {statusLabel[overview.currentStatus]}
        </p>
        <p class="text-xs text-muted-foreground">Latest Status</p>
      </div>
      {#if overview.lastLatencyMs}
        <div class="flex flex-col items-end gap-1">
          <p class="text-2xl font-semibold">{formatLatency(overview.lastLatencyMs)}</p>
          <p class="text-xs text-muted-foreground">Latest Latency</p>
        </div>
      {/if}
    </div>
  </div>

  <!-- Detail card with tabs -->
  <div class="bg-background rounded-3xl border transition-opacity {loading ? 'opacity-50' : ''}">
    <Tabs.Root value="status" class="w-full">
      <!-- Tab header with day selector -->
      <div class="flex items-center justify-between px-5 pt-4 pb-0 gap-4 border-b">
        <Tabs.List class="bg-transparent p-0 h-auto rounded-none gap-0">
          {#each [["status", "Status"], ["latency", "Latency"], ["incidents", "Incidents"], ["maintenances", "Maintenances"]] as [val, label] (val)}
            <Tabs.Trigger
              value={val}
              class="relative rounded-none bg-transparent px-4 pb-3 pt-1 text-sm font-medium text-muted-foreground shadow-none transition-colors hover:text-foreground data-[state=active]:text-foreground data-[state=active]:shadow-none data-[state=active]:bg-transparent after:absolute after:bottom-0 after:left-0 after:right-0 after:h-0.5 after:rounded-full after:bg-foreground after:scale-x-0 data-[state=active]:after:scale-x-100 after:transition-transform"
            >
              {label}
              {#if val === "incidents" && data.serviceIncidents.length > 0}
                <span class="ml-1.5 inline-flex size-4 items-center justify-center rounded-full bg-muted text-[10px] text-destructive font-semibold">
                  {data.serviceIncidents.length}
                </span>
              {/if}
            </Tabs.Trigger>
          {/each}
        </Tabs.List>

        <Select.Root
          type="single"
          value={String(selectedDays)}
          onValueChange={(v) => v && changeDays(Number(v))}
        >
          <Select.Trigger class="w-28 h-8 text-xs rounded-full shrink-0 mb-2">
            {DAY_OPTIONS.find(o => o.value === selectedDays)?.label ?? `${selectedDays} Days`}
          </Select.Trigger>
          <Select.Content>
            {#each DAY_OPTIONS as opt (opt.value)}
              <Select.Item value={String(opt.value)}>{opt.label}</Select.Item>
            {/each}
          </Select.Content>
        </Select.Root>
      </div>

      <!-- Status tab -->
      <Tabs.Content value="status" class="p-5 flex flex-col gap-5 mt-0">
        <!-- Uptime -->
        <div class="flex flex-col gap-0.5">
          <p class="text-3xl font-bold">{overview.uptimePercent.toFixed(4)}%</p>
          <p class="text-xs text-muted-foreground">Uptime</p>
        </div>

        <!-- N-day bar -->
        {#if overview.dailyData.length > 0}
          <div class="flex flex-col gap-1">
            <StatusBarCalendar data={overview.dailyData} onDayClick={openDayDetail} />
            <div class="flex justify-between text-xs text-muted-foreground mt-1">
              <span>{fromDate}</span>
              <span>{toDate}</span>
            </div>
          </div>
        {:else}
          <div class="h-12 rounded-lg bg-muted/50 flex items-center justify-center text-xs text-muted-foreground">
            No history data yet
          </div>
        {/if}

      </Tabs.Content>

      <!-- Latency tab -->
      <Tabs.Content value="latency" class="p-5 flex flex-col gap-5 mt-0">
        {#if overview.overallAvgLatencyMs !== null}
          <div class="flex items-center gap-2">
            <p class="text-xs text-muted-foreground font-medium">Latency Trend</p>
            <div class="flex gap-1">
              {#each ([["avg", "Avg Latency"], ["min", "Min Latency"], ["max", "Max Latency"]] as const) as [val, label] (val)}
                <button
                  class="text-xs px-2 py-0.5 rounded-full border transition-colors {latencyMetric === val ? 'bg-foreground text-background border-foreground' : 'text-muted-foreground hover:text-foreground border-border'}"
                  onclick={() => latencyMetric = val}
                >
                  {label}
                </button>
              {/each}
            </div>
          </div>

          <div class="grid grid-cols-3 gap-4">
            <div class="flex flex-col gap-0.5">
              <p class="text-xl font-bold">{formatLatency(overview.overallMinLatencyMs)}</p>
              <p class="text-xs text-muted-foreground">Min Latency</p>
            </div>
            <div class="flex flex-col items-center gap-0.5">
              <p class="text-xl font-bold">{formatLatency(overview.overallAvgLatencyMs)}</p>
              <p class="text-xs text-muted-foreground">Average Latency</p>
            </div>
            <div class="flex flex-col items-end gap-0.5">
              <p class="text-xl font-bold">{formatLatency(overview.overallMaxLatencyMs)}</p>
              <p class="text-xs text-muted-foreground">Max Latency</p>
            </div>
          </div>

          <LatencyTrendChart data={overview.dailyData} metric={latencyMetric} />
        {:else}
          <p class="text-sm text-muted-foreground py-8 text-center">No latency data available</p>
        {/if}
      </Tabs.Content>

      <!-- Incidents tab -->
      <Tabs.Content value="incidents" class="p-5 flex flex-col gap-3 mt-0">
        {#if data.serviceIncidents.length === 0}
          <p class="text-sm text-muted-foreground text-center py-8">No incidents recorded</p>
        {:else}
          {#each data.serviceIncidents as incident (incident.id)}
            <IncidentCard {incident} />
          {/each}
        {/if}
      </Tabs.Content>

      <!-- Maintenances tab -->
      <Tabs.Content value="maintenances" class="p-5 flex flex-col gap-3 mt-0">
        {#if data.serviceMaintenances.length === 0}
          <p class="text-sm text-muted-foreground text-center py-8">No maintenances scheduled</p>
        {:else}
          {#each data.serviceMaintenances as m (m.id)}
            <MaintenanceCard maintenance={m} />
          {/each}
        {/if}
      </Tabs.Content>
    </Tabs.Root>
  </div>
</main>

<!-- Day detail dialog -->
<Dialog.Root bind:open={dayDetailOpen}>
  <Dialog.Portal>
    <Dialog.Overlay />
    <Dialog.Content class="max-w-2xl w-full max-h-[90vh] overflow-y-auto">
      <Dialog.Header>
        <Dialog.Title>{dayDetailDay ? formatDayHeader(dayDetailDay.timestamp) : ""}</Dialog.Title>
        <Dialog.Description>Minute-by-minute status data for this day</Dialog.Description>
      </Dialog.Header>

      {#if dayDetailLoading}
        <div class="py-12 text-center text-sm text-muted-foreground">Loading…</div>
      {:else}
        <PerMinuteStatusGrid history={dayDetailHistory} />
      {/if}
    </Dialog.Content>
  </Dialog.Portal>
</Dialog.Root>
