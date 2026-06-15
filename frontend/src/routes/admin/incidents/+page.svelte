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
  import SirenIcon from "@lucide/svelte/icons/siren";
  import { goto } from "$app/navigation";
  import { formatDistanceToNow } from "date-fns";
  import type { Incident } from "$lib/api";

  let loading = $state(true);
  let incidents = $state<Incident[]>([]);
  let totalPages = $state(0);
  let totalCount = $state(0);
  let pageNo = $state(1);
  let stateFilter = $state("ALL");
  const limit = 10;

  const stateOptions = [
    { value: "ALL", label: "All States" },
    { value: "Investigating", label: "Investigating" },
    { value: "Identified", label: "Identified" },
    { value: "Monitoring", label: "Monitoring" },
    { value: "Resolved", label: "Resolved" },
  ];

  async function fetchData() {
    loading = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "getIncidents",
          data: { includeResolved: stateFilter === "Resolved" || stateFilter === "ALL" },
        }),
      });
      const result = await res.json();
      if (!result.error) {
        let all: Incident[] = result.incidents;
        if (stateFilter !== "ALL") all = all.filter((i) => i.state === stateFilter);
        totalCount = all.length;
        totalPages = Math.max(1, Math.ceil(totalCount / limit));
        incidents = all.slice((pageNo - 1) * limit, pageNo * limit);
      }
    } catch (e) {
      console.error(e);
    } finally {
      loading = false;
    }
  }

  function formatDuration(i: Incident): string {
    if (!i.endDateTime) return formatDistanceToNow(new Date(i.startDateTime * 1000));
    const ms = (i.endDateTime - i.startDateTime) * 1000;
    const mins = Math.floor(ms / 60000);
    if (mins < 60) return `${mins}m`;
    const hrs = Math.floor(mins / 60);
    return hrs < 24 ? `${hrs}h ${mins % 60}m` : `${Math.floor(hrs / 24)}d ${hrs % 24}h`;
  }

  function stateBadgeVariant(state: string): "default" | "secondary" | "destructive" | "outline" {
    return state === "Resolved" ? "default"
      : state === "Monitoring" ? "secondary"
      : state === "Identified" ? "outline"
      : "destructive";
  }

  function handleStateFilter(v: string | undefined) {
    if (v) { stateFilter = v; pageNo = 1; fetchData(); }
  }

  function goToPage(p: number) { pageNo = p; fetchData(); }

  $effect(() => { fetchData(); });
</script>

<div class="container mx-auto space-y-6 py-6">
  <div class="flex items-center justify-between">
    <div class="flex items-center gap-3">
      <Select.Root type="single" value={stateFilter} onValueChange={handleStateFilter}>
        <Select.Trigger class="w-44">
          {stateOptions.find((o) => o.value === stateFilter)?.label || "All States"}
        </Select.Trigger>
        <Select.Content>
          {#each stateOptions as option (option.value)}
            <Select.Item value={option.value}>{option.label}</Select.Item>
          {/each}
        </Select.Content>
      </Select.Root>
      {#if loading}<Spinner class="size-5" />{/if}
    </div>
    <Button href="/admin/incidents/new">
      <PlusIcon class="size-4" />
      New Incident
    </Button>
  </div>

  <div class="ktable rounded-2xl border">
    <Table.Root>
      <Table.Header>
        <Table.Row>
          <Table.Head class="w-16">ID</Table.Head>
          <Table.Head>Title</Table.Head>
          <Table.Head class="w-32">Duration</Table.Head>
          <Table.Head class="w-36">State</Table.Head>
          <Table.Head class="w-40">Affects</Table.Head>
          <Table.Head class="w-24 text-right">Actions</Table.Head>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        {#if incidents.length === 0 && !loading}
          <Table.Row>
            <Table.Cell colspan={6} class="text-muted-foreground py-8 text-center">No incidents found</Table.Cell>
          </Table.Row>
        {:else}
          {#each incidents as incident (incident.id)}
            <Table.Row
              class="hover:bg-muted/50 cursor-pointer"
              onclick={() => goto(`/admin/incidents/${incident.id}`)}
            >
              <Table.Cell class="font-medium">{incident.id}</Table.Cell>
              <Table.Cell>
                <Tooltip.Root>
                  <Tooltip.Trigger>
                    <span class="line-clamp-1 max-w-xs">{incident.title}</span>
                  </Tooltip.Trigger>
                  <Tooltip.Content><p class="max-w-md">{incident.title}</p></Tooltip.Content>
                </Tooltip.Root>
              </Table.Cell>
              <Table.Cell>
                <span class="text-muted-foreground text-sm">{formatDuration(incident)}</span>
              </Table.Cell>
              <Table.Cell>
                <Badge variant={stateBadgeVariant(incident.state)} class="gap-1">
                  <SirenIcon class="size-3" />
                  {incident.state}
                </Badge>
              </Table.Cell>
              <Table.Cell>
                {#if incident.services?.length}
                  <Tooltip.Root>
                    <Tooltip.Trigger>
                      <Badge variant="outline">{incident.services.length} service(s)</Badge>
                    </Tooltip.Trigger>
                    <Tooltip.Content>
                      <div class="space-y-1">
                        {#each incident.services as s (s.serviceSlug)}
                          <div class="text-sm">
                            <span class="font-medium">{s.serviceSlug}</span>
                            <span class="text-muted-foreground ml-1">({s.impact})</span>
                          </div>
                        {/each}
                      </div>
                    </Tooltip.Content>
                  </Tooltip.Root>
                {:else if incident.isGlobal}
                  <Badge variant="secondary">Global</Badge>
                {:else}
                  <span class="text-muted-foreground text-sm">None</span>
                {/if}
              </Table.Cell>
              <Table.Cell class="text-right">
                <Button
                  variant="outline"
                  size="sm"
                  onclick={(e) => { e.stopPropagation(); goto(`/admin/incidents/${incident.id}`); }}
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
      <p class="text-muted-foreground text-sm">
        Showing {(pageNo - 1) * limit + 1}–{Math.min(pageNo * limit, totalCount)} of {totalCount}
      </p>
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
