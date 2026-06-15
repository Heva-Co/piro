<script lang="ts">
  import type { PageData } from "./$types";
  import { formatStatus } from "$lib/api.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Table from "$lib/components/ui/table/index.js";
  import { goto } from "$app/navigation";
  import { format, formatDistanceToNow } from "date-fns";

  import BlendIcon from "@lucide/svelte/icons/blend";
  import CloudAlertIcon from "@lucide/svelte/icons/cloud-alert";
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";
  import TriangleAlertIcon from "@lucide/svelte/icons/triangle-alert";
  import ArrowRightIcon from "@lucide/svelte/icons/arrow-right";
  import WrenchIcon from "@lucide/svelte/icons/wrench";
  import CalendarClockIcon from "@lucide/svelte/icons/calendar-clock";
  import ActivityIcon from "@lucide/svelte/icons/activity";

  let { data }: { data: PageData } = $props();

  const upCount         = $derived(data.services.filter(s => s.currentStatus === "UP").length);
  const issueCount      = $derived(data.services.filter(s => ["DOWN","DEGRADED"].includes(s.currentStatus)).length);
  const activeIncidents = $derived(data.incidents.filter(i => i.status === "Active"));
  const activeMaintenances = $derived(
    data.maintenances.filter(m =>
      m.status === "Active" &&
      m.upcomingEvents?.some(e => e.status === "Ongoing" || e.status === "Scheduled")
    )
  );

  const statusColor: Record<string, string> = {
    UP:          "text-green-600 bg-green-50 dark:bg-green-950/40",
    DOWN:        "text-red-600 bg-red-50 dark:bg-red-950/40",
    DEGRADED:    "text-amber-600 bg-amber-50 dark:bg-amber-950/40",
    MAINTENANCE: "text-blue-600 bg-blue-50 dark:bg-blue-950/40",
    NO_DATA:     "text-muted-foreground bg-secondary",
  };

  const incidentStateColor: Record<string, string> = {
    Investigating: "text-amber-600",
    Identified:    "text-orange-600",
    Monitoring:    "text-blue-600",
    Resolved:      "text-green-600",
  };

  function fmtTs(ts: number) {
    return format(new Date(ts * 1000), "MMM d, yyyy HH:mm");
  }

  function relativeTs(ts: number) {
    return formatDistanceToNow(new Date(ts * 1000), { addSuffix: true });
  }
</script>


<div class="container mx-auto py-8 flex flex-col gap-8">

  <!-- ── Stat cards ─────────────────────────────────────────────────────────── -->
  <div class="grid grid-cols-2 gap-4 @xl/main:grid-cols-4">
    <!-- Total services -->
    <div class="rounded-2xl border bg-card p-5 flex items-start justify-between gap-3">
      <div class="flex flex-col gap-1">
        <span class="text-xs font-medium text-muted-foreground uppercase tracking-wide">Services</span>
        <span class="text-4xl font-bold tracking-tight">{data.services.length}</span>
      </div>
      <div class="rounded-xl bg-secondary p-2.5 mt-0.5">
        <BlendIcon class="size-5 text-muted-foreground" />
      </div>
    </div>

    <!-- Operational -->
    <div class="rounded-2xl border bg-card p-5 flex items-start justify-between gap-3">
      <div class="flex flex-col gap-1">
        <span class="text-xs font-medium text-muted-foreground uppercase tracking-wide">Operational</span>
        <span class="text-4xl font-bold tracking-tight text-green-600">{upCount}</span>
      </div>
      <div class="rounded-xl bg-green-50 dark:bg-green-950/40 p-2.5 mt-0.5">
        <CircleCheckIcon class="size-5 text-green-600" />
      </div>
    </div>

    <!-- With issues -->
    <div class="rounded-2xl border bg-card p-5 flex items-start justify-between gap-3">
      <div class="flex flex-col gap-1">
        <span class="text-xs font-medium text-muted-foreground uppercase tracking-wide">With Issues</span>
        <span class="text-4xl font-bold tracking-tight text-red-600">{issueCount}</span>
      </div>
      <div class="rounded-xl bg-red-50 dark:bg-red-950/40 p-2.5 mt-0.5">
        <TriangleAlertIcon class="size-5 text-red-600" />
      </div>
    </div>

    <!-- Active incidents -->
    <div class="rounded-2xl border bg-card p-5 flex items-start justify-between gap-3">
      <div class="flex flex-col gap-1">
        <span class="text-xs font-medium text-muted-foreground uppercase tracking-wide">Active Incidents</span>
        <span class="text-4xl font-bold tracking-tight text-amber-600">{activeIncidents.length}</span>
      </div>
      <div class="rounded-xl bg-amber-50 dark:bg-amber-950/40 p-2.5 mt-0.5">
        <CloudAlertIcon class="size-5 text-amber-600" />
      </div>
    </div>
  </div>

  <!-- ── Two-column lower section ───────────────────────────────────────────── -->
  <div class="grid grid-cols-1 gap-6 @3xl/main:grid-cols-3">

    <!-- Services table (2/3) -->
    <div class="@3xl/main:col-span-2 rounded-2xl border bg-card overflow-hidden flex flex-col">
      <div class="flex items-center justify-between px-5 py-4 border-b">
        <div class="flex items-center gap-2">
          <BlendIcon class="size-4 text-muted-foreground" />
          <span class="font-semibold text-sm">Services</span>
        </div>
        <Button variant="ghost" size="sm" href="/admin/services" class="gap-1 text-xs">
          Manage <ArrowRightIcon class="size-3.5" />
        </Button>
      </div>

      {#if data.services.length === 0}
        <div class="px-6 py-10 text-center text-sm text-muted-foreground">
          No services yet. <a href="/admin/services" class="underline">Add one →</a>
        </div>
      {:else}
        <Table.Root>
          <Table.Header>
            <Table.Row class="bg-secondary/30">
              <Table.Head class="pl-5">Service</Table.Head>
              <Table.Head class="w-36">Status</Table.Head>
              <Table.Head class="w-28 text-right pr-5">Checks</Table.Head>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {#each data.services as s (s.id)}
              <Table.Row
                class="cursor-pointer hover:bg-muted/40 transition-colors"
                onclick={() => goto(`/admin/services/${s.slug}`)}
              >
                <Table.Cell class="pl-5">
                  <div class="font-medium text-sm">{s.name}</div>
                  {#if s.description}
                    <div class="text-xs text-muted-foreground line-clamp-1 max-w-xs">{s.description}</div>
                  {/if}
                </Table.Cell>
                <Table.Cell>
                  <span class="inline-flex items-center gap-1.5 text-xs font-semibold rounded-full px-2.5 py-0.5 {statusColor[s.currentStatus] ?? statusColor.NO_DATA}">
                    {#if s.currentStatus === "UP"}<CircleCheckIcon class="size-3" />
                    {:else if s.currentStatus === "DOWN"}<TriangleAlertIcon class="size-3" />
                    {:else if s.currentStatus === "DEGRADED"}<TriangleAlertIcon class="size-3" />
                    {:else if s.currentStatus === "MAINTENANCE"}<WrenchIcon class="size-3" />
                    {/if}
                    {formatStatus(s.currentStatus)}
                  </span>
                </Table.Cell>
                <Table.Cell class="text-right pr-5">
                  <Button
                    variant="ghost"
                    size="sm"
                    class="h-7 text-xs"
                    onclick={(e) => { e.stopPropagation(); goto(`/admin/services/${s.slug}`); }}
                  >
                    View
                  </Button>
                </Table.Cell>
              </Table.Row>
            {/each}
          </Table.Body>
        </Table.Root>
      {/if}
    </div>

    <!-- Right sidebar (1/3) -->
    <div class="flex flex-col gap-4">

      <!-- Active Incidents -->
      <div class="rounded-2xl border bg-card overflow-hidden">
        <div class="flex items-center justify-between px-5 py-4 border-b">
          <div class="flex items-center gap-2">
            <CloudAlertIcon class="size-4 text-muted-foreground" />
            <span class="font-semibold text-sm">Active Incidents</span>
          </div>
          <Button variant="ghost" size="sm" href="/admin/incidents" class="gap-1 text-xs">
            All <ArrowRightIcon class="size-3.5" />
          </Button>
        </div>
        {#if activeIncidents.length === 0}
          <div class="px-5 py-6 text-center text-xs text-muted-foreground flex flex-col items-center gap-2">
            <ActivityIcon class="size-7 opacity-30" />
            No active incidents
          </div>
        {:else}
          <div class="divide-y">
            {#each activeIncidents.slice(0, 5) as incident (incident.id)}
              <a
                href="/admin/incidents/{incident.id}"
                class="flex flex-col gap-0.5 px-5 py-3 hover:bg-muted/40 transition-colors"
              >
                <div class="flex items-center justify-between gap-2">
                  <span class="text-xs font-semibold uppercase tracking-wide {incidentStateColor[incident.state] ?? ''}">
                    {incident.state}
                  </span>
                  <span class="text-xs text-muted-foreground shrink-0">{relativeTs(incident.startDateTime)}</span>
                </div>
                <span class="text-sm font-medium line-clamp-1">{incident.title}</span>
              </a>
            {/each}
          </div>
        {/if}
      </div>

      <!-- Active Maintenances -->
      <div class="rounded-2xl border bg-card overflow-hidden">
        <div class="flex items-center justify-between px-5 py-4 border-b">
          <div class="flex items-center gap-2">
            <CalendarClockIcon class="size-4 text-muted-foreground" />
            <span class="font-semibold text-sm">Active Maintenances</span>
          </div>
          <Button variant="ghost" size="sm" href="/admin/maintenances" class="gap-1 text-xs">
            All <ArrowRightIcon class="size-3.5" />
          </Button>
        </div>
        {#if activeMaintenances.length === 0}
          <div class="px-5 py-6 text-center text-xs text-muted-foreground flex flex-col items-center gap-2">
            <CalendarClockIcon class="size-7 opacity-30" />
            No active maintenances
          </div>
        {:else}
          <div class="divide-y">
            {#each activeMaintenances.slice(0, 5) as m (m.id)}
              {#each [m.upcomingEvents?.[0]] as ev (m.id)}
                <a
                  href="/admin/maintenances/{m.id}"
                  class="flex flex-col gap-0.5 px-5 py-3 hover:bg-muted/40 transition-colors"
                >
                  <div class="flex items-center justify-between gap-2">
                    <span class="text-xs font-semibold uppercase tracking-wide text-blue-600">
                      {ev?.status ?? "Scheduled"}
                    </span>
                    {#if ev}
                      <span class="text-xs text-muted-foreground shrink-0">{fmtTs(ev.startDateTime)}</span>
                    {/if}
                  </div>
                  <span class="text-sm font-medium line-clamp-1">{m.title}</span>
                </a>
              {/each}
            {/each}
          </div>
        {/if}
      </div>

    </div>
  </div>
</div>
