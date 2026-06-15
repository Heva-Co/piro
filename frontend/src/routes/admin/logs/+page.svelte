<script lang="ts">
  import { onMount } from "svelte";
  import * as Table from "$lib/components/ui/table/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import * as Tooltip from "$lib/components/ui/tooltip/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import RefreshCwIcon from "@lucide/svelte/icons/refresh-cw";
  import ChevronLeftIcon from "@lucide/svelte/icons/chevron-left";
  import ChevronRightIcon from "@lucide/svelte/icons/chevron-right";
  import type { LogPageDto } from "$lib/api.js";
  import { format } from "date-fns";

  const LEVELS = ["", "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

  let data = $state<LogPageDto | null>(null);
  let loading = $state(false);
  let error = $state("");
  let level = $state("");
  let search = $state("");
  let page = $state(1);
  const PAGE_SIZE = 50;

  let searchTimeout: ReturnType<typeof setTimeout>;

  function levelVariant(l: string): "default" | "secondary" | "outline" | "destructive" {
    if (l === "Error" || l === "Fatal") return "destructive";
    if (l === "Warning") return "default";
    if (l === "Information") return "secondary";
    return "outline";
  }

  async function load() {
    loading = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "getLogs",
          data: { level: level || undefined, search: search || undefined, page, pageSize: PAGE_SIZE },
        }),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else data = result;
    } catch { error = "Failed to load logs."; }
    finally { loading = false; }
  }

  function onSearchInput() {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => { page = 1; load(); }, 400);
  }

  function onFilterChange() {
    page = 1;
    load();
  }

  onMount(load);
</script>


<div class="flex w-full flex-col gap-6 p-4">
  <div class="flex items-center justify-between">
    <div>
      <h1 class="text-xl font-semibold">Application Logs</h1>
      <p class="text-sm text-muted-foreground">Structured log entries emitted by Piro.</p>
    </div>
    <Button variant="outline" size="sm" onclick={load} disabled={loading}>
      <RefreshCwIcon class="size-4 mr-1" /> Refresh
    </Button>
  </div>

  <!-- Filters -->
  <div class="flex items-center gap-3 flex-wrap">
    <Select.Root type="single" value={level} onValueChange={(v) => { level = v ?? ""; onFilterChange(); }}>
      <Select.Trigger class="w-36">
        {level || "All levels"}
      </Select.Trigger>
      <Select.Content>
        <Select.Item value="">All levels</Select.Item>
        {#each LEVELS.filter(Boolean) as l (l)}
          <Select.Item value={l}>{l}</Select.Item>
        {/each}
      </Select.Content>
    </Select.Root>

    <Input
      class="w-72"
      placeholder="Search message or source…"
      bind:value={search}
      oninput={onSearchInput}
    />

    {#if data}
      <span class="text-xs text-muted-foreground ml-auto">{data.totalCount.toLocaleString()} entries</span>
    {/if}
  </div>

  {#if error}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Description>{error}</Alert.Description>
    </Alert.Root>
  {/if}

  {#if loading && !data}
    <div class="flex justify-center py-16"><Spinner class="size-6" /></div>
  {:else if data}
    <div class="rounded-lg border overflow-hidden relative">
      {#if loading}
        <div class="absolute inset-0 bg-background/50 flex items-center justify-center z-10">
          <Spinner class="size-5" />
        </div>
      {/if}
      <Table.Root>
        <Table.Header>
          <Table.Row>
            <Table.Head class="w-40">Time</Table.Head>
            <Table.Head class="w-24">Level</Table.Head>
            <Table.Head class="w-52">Source</Table.Head>
            <Table.Head>Message</Table.Head>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {#if data.items.length === 0}
            <Table.Row>
              <Table.Cell colspan={4} class="text-center py-12 text-muted-foreground text-sm">
                No log entries found.
              </Table.Cell>
            </Table.Row>
          {/if}
          {#each data.items as log (log.id)}
            <Table.Row class="{log.level === 'Error' || log.level === 'Fatal' ? 'bg-destructive/5' : log.level === 'Warning' ? 'bg-amber-500/5' : ''}">
              <Table.Cell class="font-mono text-xs text-muted-foreground whitespace-nowrap">
                {format(new Date(log.timestamp), "MMM d, HH:mm:ss")}
              </Table.Cell>
              <Table.Cell>
                <Badge variant={levelVariant(log.level)} class="text-xs">{log.level}</Badge>
              </Table.Cell>
              <Table.Cell class="font-mono text-xs text-muted-foreground truncate max-w-52" title={log.sourceContext ?? ""}>
                {log.sourceContext?.split(".").pop() ?? ""}
              </Table.Cell>
              <Table.Cell class="text-sm">
                <div class="space-y-0.5">
                  <p class="leading-snug">{log.message}</p>
                  {#if log.exception}
                    <Tooltip.Provider>
                      <Tooltip.Root>
                        <Tooltip.Trigger>
                          <p class="text-xs text-destructive font-mono truncate max-w-xl cursor-help">
                            {log.exception.split("\n")[0]}
                          </p>
                        </Tooltip.Trigger>
                        <Tooltip.Content class="max-w-2xl">
                          <pre class="text-xs whitespace-pre-wrap">{log.exception}</pre>
                        </Tooltip.Content>
                      </Tooltip.Root>
                    </Tooltip.Provider>
                  {/if}
                </div>
              </Table.Cell>
            </Table.Row>
          {/each}
        </Table.Body>
      </Table.Root>
    </div>

    <!-- Pagination -->
    {#if data.totalCount > PAGE_SIZE}
      {@const totalPages = Math.ceil(data.totalCount / PAGE_SIZE)}
      <div class="flex items-center justify-between">
        <p class="text-sm text-muted-foreground">
          Page {page} of {totalPages}
        </p>
        <div class="flex items-center gap-2">
          <Button variant="outline" size="sm" disabled={page <= 1} onclick={() => { page--; load(); }}>
            <ChevronLeftIcon class="size-4" /> Previous
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onclick={() => { page++; load(); }}>
            Next <ChevronRightIcon class="size-4" />
          </Button>
        </div>
      </div>
    {/if}
  {/if}
</div>
