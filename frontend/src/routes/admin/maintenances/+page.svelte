<script lang="ts">
  import { Button } from "$lib/components/ui/button/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Table from "$lib/components/ui/table/index.js";
  import * as Tooltip from "$lib/components/ui/tooltip/index.js";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import ChevronLeftIcon from "@lucide/svelte/icons/chevron-left";
  import ChevronRightIcon from "@lucide/svelte/icons/chevron-right";
  import PencilIcon from "@lucide/svelte/icons/pencil";
  import CalendarIcon from "@lucide/svelte/icons/calendar";
  import RepeatIcon from "@lucide/svelte/icons/repeat";
  import ClockIcon from "@lucide/svelte/icons/clock";
  import BlendIcon from "@lucide/svelte/icons/blend";
  import { goto } from "$app/navigation";
  import { format, formatDistanceToNow, isWithinInterval, isFuture, isPast } from "date-fns";
  import type { Maintenance, MaintenanceEvent } from "$lib/api";

  let loading = $state(true);
  let maintenances = $state<Maintenance[]>([]);
  let totalPages = $state(0);
  let totalCount = $state(0);
  let pageNo = $state(1);
  let statusFilter = $state("Active");
  const limit = 10;

  async function fetchData() {
    loading = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getMaintenances", data: {} }),
      });
      const result = await res.json();
      if (!result.error) {
        let all: Maintenance[] = result.maintenances;
        if (statusFilter !== "ALL") all = all.filter((m) => m.status === statusFilter);
        totalCount = all.length;
        totalPages = Math.max(1, Math.ceil(totalCount / limit));
        maintenances = all.slice((pageNo - 1) * limit, pageNo * limit);
      }
    } catch (e) { console.error(e); }
    finally { loading = false; }
  }

  function formatDuration(s: number): string {
    if (s < 60) return `${s}s`;
    const m = Math.floor(s / 60);
    if (m < 60) return `${m}m`;
    const h = Math.floor(m / 60);
    return m % 60 === 0 ? `${h}h` : `${h}h ${m % 60}m`;
  }

  function isRecurring(rRule: string): boolean {
    return !!rRule && !rRule.includes("COUNT=1");
  }

  function eventStatus(ev: MaintenanceEvent): { label: string; variant: "default" | "secondary" | "outline" } {
    const now = new Date();
    const start = new Date(ev.startDateTime * 1000);
    const end = new Date(ev.endDateTime * 1000);
    if (isWithinInterval(now, { start, end })) return { label: "Ongoing", variant: "default" };
    if (isFuture(start)) return { label: `In ${formatDistanceToNow(start)}`, variant: "outline" };
    if (isPast(end)) return { label: "Completed", variant: "secondary" };
    return { label: "Scheduled", variant: "outline" };
  }

  function handleStatusFilter(v: string | undefined) {
    if (v) { statusFilter = v; pageNo = 1; fetchData(); }
  }

  function goToPage(p: number) { pageNo = p; fetchData(); }

  $effect(() => { fetchData(); });
</script>

<div class="container mx-auto space-y-6 py-6">
  <div class="flex items-center justify-between">
    <div class="flex items-center gap-3">
      <Select.Root type="single" value={statusFilter} onValueChange={handleStatusFilter}>
        <Select.Trigger class="w-40">
          {statusFilter === "ALL" ? "All" : statusFilter === "Active" ? "Active" : "Cancelled"}
        </Select.Trigger>
        <Select.Content>
          <Select.Item value="ALL">All</Select.Item>
          <Select.Item value="Active">Active</Select.Item>
          <Select.Item value="Cancelled">Cancelled</Select.Item>
        </Select.Content>
      </Select.Root>
      {#if loading}<Spinner class="size-5" />{/if}
    </div>
    <Button href="/admin/maintenances/new">
      <PlusIcon class="size-4" />
      New Maintenance
    </Button>
  </div>

  <div class="ktable rounded-xl border">
    <Table.Root>
      <Table.Header>
        <Table.Row>
          <Table.Head class="w-16">ID</Table.Head>
          <Table.Head>Title</Table.Head>
          <Table.Head class="w-32">Type</Table.Head>
          <Table.Head class="w-36">Duration</Table.Head>
          <Table.Head class="w-24">Services</Table.Head>
          <Table.Head class="w-40">Next Event</Table.Head>
          <Table.Head class="w-24">Status</Table.Head>
          <Table.Head class="w-24 text-right">Actions</Table.Head>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        {#if maintenances.length === 0 && !loading}
          <Table.Row>
            <Table.Cell colspan={8} class="text-muted-foreground py-8 text-center">No maintenances found</Table.Cell>
          </Table.Row>
        {:else}
          {#each maintenances as m (m.id)}
            <Table.Row class="hover:bg-muted/50 cursor-pointer" onclick={() => goto(`/admin/maintenances/${m.id}`)}>
              <Table.Cell class="font-medium">{m.id}</Table.Cell>
              <Table.Cell>
                <Tooltip.Root>
                  <Tooltip.Trigger>
                    <span class="line-clamp-1 max-w-xs">{m.title}</span>
                  </Tooltip.Trigger>
                  <Tooltip.Content>
                    <p class="max-w-md">{m.title}</p>
                    {#if m.description}<p class="text-muted-foreground mt-1 text-sm">{m.description}</p>{/if}
                  </Tooltip.Content>
                </Tooltip.Root>
              </Table.Cell>
              <Table.Cell>
                <Badge variant="outline" class="gap-1">
                  {#if isRecurring(m.rRule)}
                    <RepeatIcon class="size-3" /> Recurring
                  {:else}
                    <CalendarIcon class="size-3" /> One-Time
                  {/if}
                </Badge>
              </Table.Cell>
              <Table.Cell>
                <Tooltip.Root>
                  <Tooltip.Trigger>
                    <div class="flex items-center gap-1">
                      <ClockIcon class="text-muted-foreground size-3" />
                      <span class="text-muted-foreground text-sm">{formatDuration(m.durationSeconds)}</span>
                    </div>
                  </Tooltip.Trigger>
                  <Tooltip.Content>
                    <div class="text-sm">
                      <div><span class="text-muted-foreground">Start:</span> {format(new Date(m.startDateTime * 1000), "yyyy-MM-dd HH:mm")}</div>
                      <div><span class="text-muted-foreground">Duration:</span> {formatDuration(m.durationSeconds)}</div>
                      {#if m.rRule}<div><span class="text-muted-foreground">RRULE:</span> {m.rRule}</div>{/if}
                    </div>
                  </Tooltip.Content>
                </Tooltip.Root>
              </Table.Cell>
              <Table.Cell>
                {#if m.serviceSlugs?.length}
                  <Tooltip.Root>
                    <Tooltip.Trigger>
                      <div class="flex items-center gap-1">
                        <BlendIcon class="text-muted-foreground size-3" />
                        <span class="text-sm">{m.serviceSlugs.length}</span>
                      </div>
                    </Tooltip.Trigger>
                    <Tooltip.Content>
                      {#each m.serviceSlugs as slug (slug)}<div class="text-sm">{slug}</div>{/each}
                    </Tooltip.Content>
                  </Tooltip.Root>
                {:else if m.isGlobal}
                  <Badge variant="secondary">Global</Badge>
                {:else}
                  <span class="text-muted-foreground text-sm">None</span>
                {/if}
              </Table.Cell>
              <Table.Cell>
                {#if m.upcomingEvents?.length}
                  {@const ev = m.upcomingEvents[0]}
                  {@const ds = eventStatus(ev)}
                  <Tooltip.Root>
                    <Tooltip.Trigger><Badge variant={ds.variant}>{ds.label}</Badge></Tooltip.Trigger>
                    <Tooltip.Content>
                      <div class="text-sm">
                        <div><span class="text-muted-foreground">Start:</span> {format(new Date(ev.startDateTime * 1000), "MMM d HH:mm")}</div>
                        <div><span class="text-muted-foreground">End:</span> {format(new Date(ev.endDateTime * 1000), "MMM d HH:mm")}</div>
                      </div>
                    </Tooltip.Content>
                  </Tooltip.Root>
                {:else}
                  <span class="text-muted-foreground text-sm">No events</span>
                {/if}
              </Table.Cell>
              <Table.Cell>
                <Badge variant={m.status === "Active" ? "default" : "secondary"}>{m.status}</Badge>
              </Table.Cell>
              <Table.Cell class="text-right">
                <Button
                  variant="outline"
                  size="sm"
                  onclick={(e) => { e.stopPropagation(); goto(`/admin/maintenances/${m.id}`); }}
                >
                  <PencilIcon class="size-4" /> Edit
                </Button>
              </Table.Cell>
            </Table.Row>
          {/each}
        {/if}
      </Table.Body>
    </Table.Root>
  </div>

  {#if totalCount > 0}
    <div class="flex items-center justify-between">
      <span class="text-muted-foreground text-sm">
        Showing {(pageNo - 1) * limit + 1}–{Math.min(pageNo * limit, totalCount)} of {totalCount}
      </span>
      {#if totalPages > 1}
        <div class="flex items-center gap-2">
          <Button variant="outline" size="icon" disabled={pageNo === 1} onclick={() => goToPage(pageNo - 1)}>
            <ChevronLeftIcon class="size-4" />
          </Button>
          <div class="flex items-center gap-1">
            {#each Array.from({ length: totalPages }, (_, i) => i + 1) as p (p)}
              <Button variant={p === pageNo ? "default" : "ghost"} size="sm" onclick={() => goToPage(p)}>{p}</Button>
            {/each}
          </div>
          <Button variant="outline" size="icon" disabled={pageNo === totalPages} onclick={() => goToPage(pageNo + 1)}>
            <ChevronRightIcon class="size-4" />
          </Button>
        </div>
      {/if}
    </div>
  {/if}
</div>
