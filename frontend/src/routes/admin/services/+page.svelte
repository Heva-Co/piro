<script lang="ts">
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import * as Table from "$lib/components/ui/table/index.js";
  import ChevronLeftIcon from "@lucide/svelte/icons/chevron-left";
  import ChevronRightIcon from "@lucide/svelte/icons/chevron-right";
  import Plus from "@lucide/svelte/icons/plus";
  import SettingsIcon from "@lucide/svelte/icons/settings";
  import FilterIcon from "@lucide/svelte/icons/filter";
  import XIcon from "@lucide/svelte/icons/x";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { onMount } from "svelte";
  import type { ServiceDto } from "$lib/api";

  let services = $state<ServiceDto[]>([]);
  let loading = $state(true);
  let error = $state<string | null>(null);
  let showFilters = $state(false);
  let searchQuery = $state("");
  let pageNo = $state(1);
  const limit = 10;

  const filtered = $derived(
    searchQuery.trim()
      ? services.filter(
          (s) =>
            s.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
            s.slug.toLowerCase().includes(searchQuery.toLowerCase())
        )
      : services
  );

  const totalCount = $derived(filtered.length);
  const totalPages = $derived(Math.max(1, Math.ceil(totalCount / limit)));

  const paginated = $derived.by(() => {
    const safe = Math.min(pageNo, totalPages);
    return filtered.slice((safe - 1) * limit, safe * limit);
  });

  const hasActiveFilters = $derived(searchQuery.trim() !== "");

  function goToPage(p: number) {
    if (p < 1 || p > totalPages) return;
    pageNo = p;
  }

  function clearFilters() {
    searchQuery = "";
    pageNo = 1;
  }

  function initials(name: string): string {
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((w) => w[0]?.toUpperCase() ?? "")
      .join("");
  }

  async function fetchServices() {
    loading = true;
    error = null;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getServices", data: {} }),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else services = result;
    } catch (e) {
      error = e instanceof Error ? e.message : "Failed to fetch services";
    } finally {
      loading = false;
    }
  }

  onMount(() => { fetchServices(); });
</script>

<div class="flex w-full flex-col gap-4 p-4">
  <div class="flex items-center justify-between gap-3">
    <div class="flex items-center gap-2">
      <Button
        variant={showFilters ? "default" : "outline"}
        size="sm"
        onclick={() => (showFilters = !showFilters)}
      >
        <FilterIcon class="size-4" />
        Filters
        {#if hasActiveFilters}
          <Badge variant="secondary" class="ml-1 px-1.5 py-0 text-[10px]">ON</Badge>
        {/if}
      </Button>
      {#if loading}<Spinner class="size-5 ml-2" />{/if}
    </div>
    <Button href="/admin/services/new">
      <Plus class="size-4" />
      New Service
    </Button>
  </div>

  {#if showFilters}
    <div class="bg-muted/50 flex flex-wrap items-end gap-3 rounded-lg border p-3">
      <div class="flex flex-col gap-1">
        <span class="text-muted-foreground text-xs font-medium">Search</span>
        <Input type="text" placeholder="Search by name or slug..." bind:value={searchQuery} class="w-60"
          oninput={() => (pageNo = 1)} />
      </div>
      {#if hasActiveFilters}
        <Button variant="ghost" size="sm" onclick={clearFilters}>
          <XIcon class="size-4" />
          Clear
        </Button>
      {/if}
    </div>
  {/if}

  {#if error}
    <div class="text-destructive py-8 text-center">{error}</div>
  {:else if !loading && services.length === 0}
    <div class="text-muted-foreground py-8 text-center">No services yet. Create one to get started.</div>
  {:else if !loading}
    <div class="ktable rounded-xl border">
      <Table.Root>
        <Table.Header>
          <Table.Row>
            <Table.Head>Service</Table.Head>
            <Table.Head class="w-[220px]">Slug</Table.Head>
            <Table.Head class="w-[130px]">Status</Table.Head>
            <Table.Head class="w-[100px]">Hidden</Table.Head>
            <Table.Head class="w-[160px]">Checks</Table.Head>
            <Table.Head class="w-[130px] text-right"></Table.Head>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {#each paginated as s (s.id)}
            <Table.Row>
              <Table.Cell>
                <div class="flex items-center gap-3">
                  {#if s.imageUrl}
                    <img src={s.imageUrl} alt={s.name} class="size-8 rounded-full object-cover flex-shrink-0" />
                  {:else}
                    <div class="size-8 rounded-full bg-primary/10 text-primary flex items-center justify-center text-xs font-semibold flex-shrink-0">
                      {initials(s.name)}
                    </div>
                  {/if}
                  <div>
                    <div class="font-medium">{s.name}</div>
                    {#if s.description}
                      <div class="text-muted-foreground text-xs truncate max-w-[260px]">{s.description}</div>
                    {/if}
                  </div>
                </div>
              </Table.Cell>
              <Table.Cell>
                <Badge variant="outline" class="font-mono">{s.slug}</Badge>
              </Table.Cell>
              <Table.Cell>
                <Badge
                  class={s.currentStatus === "UP"
                    ? "bg-foreground text-background hover:bg-foreground/90"
                    : s.currentStatus === "DOWN"
                    ? "bg-destructive text-destructive-foreground hover:bg-destructive/90"
                    : ""}
                  variant={s.currentStatus === "UP" || s.currentStatus === "DOWN" ? undefined : "secondary"}
                >
                  {s.currentStatus}
                </Badge>
              </Table.Cell>
              <Table.Cell>
                <Badge variant={s.isHidden ? "destructive" : "outline"}>
                  {s.isHidden ? "YES" : "NO"}
                </Badge>
              </Table.Cell>
              <Table.Cell>
                <span class="text-muted-foreground text-sm font-mono">—</span>
              </Table.Cell>
              <Table.Cell class="text-right">
                <Button variant="outline" size="sm" href="/admin/services/{s.slug}">
                  <SettingsIcon class="mr-1 size-3.5" />
                  Configure
                </Button>
              </Table.Cell>
            </Table.Row>
          {/each}
        </Table.Body>
      </Table.Root>
    </div>

    {#if totalPages > 0}
      <div class="flex items-center justify-between">
        <p class="text-muted-foreground text-sm">
          Showing {(Math.min(pageNo, totalPages) - 1) * limit + 1}–{Math.min(pageNo * limit, totalCount)} of {totalCount} services
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
  {/if}
</div>
