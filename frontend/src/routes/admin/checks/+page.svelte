<script lang="ts">
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import * as Sheet from "$lib/components/ui/sheet/index.js";
  import * as Tooltip from "$lib/components/ui/tooltip/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import SearchIcon from "@lucide/svelte/icons/search";
  import FileCodeIcon from "@lucide/svelte/icons/file-code";
  import SettingsIcon from "@lucide/svelte/icons/settings";
  import CopyIcon from "@lucide/svelte/icons/copy";
  import CheckIcon from "@lucide/svelte/icons/check";
  import { createHighlighter } from "shiki";
  import type { CheckSummaryDto, CheckDto } from "$lib/api.js";
  import { formatStatus } from "$lib/api.js";

  let checks = $state<CheckSummaryDto[]>([]);
  let loading = $state(true);
  let error = $state("");
  let search = $state("");

  // Sheet state
  let sheetOpen = $state(false);
  let sheetCheck = $state<CheckSummaryDto | null>(null);
  let sheetDetail = $state<CheckDto | null>(null);
  let sheetLoading = $state(false);
  let copied = $state(false);
  let highlightedHtml = $state<string | null>(null);

  // Shiki highlighter — created once, reused
  const highlighterPromise = createHighlighter({
    themes: ["github-dark", "github-light"],
    langs: ["yaml"],
  });

  const STATUS_COLOR: Record<string, string> = {
    UP:          "bg-green-500/15 text-green-700 dark:text-green-400 border-green-300 dark:border-green-800",
    DEGRADED:    "bg-yellow-500/15 text-yellow-700 dark:text-yellow-400 border-yellow-300 dark:border-yellow-800",
    DOWN:        "bg-red-500/15 text-red-700 dark:text-red-400 border-red-300 dark:border-red-800",
    MAINTENANCE: "bg-blue-500/15 text-blue-700 dark:text-blue-400 border-blue-300 dark:border-blue-800",
    NO_DATA:     "bg-muted text-muted-foreground",
  };

  const STATUS_DOT: Record<string, string> = {
    UP:          "bg-green-500",
    DEGRADED:    "bg-yellow-500",
    DOWN:        "bg-red-500",
    MAINTENANCE: "bg-blue-500",
    NO_DATA:     "bg-muted-foreground",
  };

  let filtered = $derived(
    search.trim()
      ? checks.filter(c =>
          c.name.toLowerCase().includes(search.toLowerCase()) ||
          c.serviceName.toLowerCase().includes(search.toLowerCase()) ||
          c.type.toLowerCase().includes(search.toLowerCase())
        )
      : checks
  );

  let stats = $derived({
    total: checks.length,
    up:    checks.filter(c => c.currentStatus === "UP").length,
    down:  checks.filter(c => c.currentStatus === "DOWN").length,
    degraded: checks.filter(c => c.currentStatus === "DEGRADED").length,
  });

  async function load() {
    loading = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getAllChecks" }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      checks = result;
    } catch { error = "Failed to load checks."; }
    finally { loading = false; }
  }

  async function openYaml(check: CheckSummaryDto) {
    sheetCheck = check;
    sheetDetail = null;
    highlightedHtml = null;
    sheetOpen = true;
    sheetLoading = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getCheck", data: { serviceSlug: check.serviceSlug, checkSlug: check.slug } }),
      });
      const result = await res.json();
      if (!result.error) {
        sheetDetail = result;
        const yaml = toYaml(check, result);
        const hl = await highlighterPromise;
        highlightedHtml = hl.codeToHtml(yaml, {
          lang: "yaml",
          themes: { dark: "github-dark", light: "github-light" },
          defaultColor: false,
        });
      }
    } finally { sheetLoading = false; }
  }

  function toYaml(check: CheckSummaryDto, detail: CheckDto): string {
    const typeData = (() => {
      try { return JSON.parse(detail.typeDataJson); } catch { return {}; }
    })();

    const lines: string[] = [
      `- slug: ${check.slug}`,
      `  name: ${check.name}`,
    ];
    if (check.description) lines.push(`  description: ${check.description}`);
    lines.push(`  type: ${check.type}`);
    lines.push(`  cron: "${check.cron}"`);
    lines.push(`  is_active: ${check.isActive}`);
    if (detail.isMultiRegion) lines.push(`  is_multi_region: true`);
    if (Object.keys(typeData).length > 0) {
      lines.push(`  type_data:`);
      for (const [k, v] of Object.entries(typeData)) {
        const val = typeof v === "string" ? v : JSON.stringify(v);
        lines.push(`    ${k}: ${val}`);
      }
    }
    return lines.join("\n");
  }

  async function copyYaml() {
    if (!sheetCheck || !sheetDetail) return;
    await navigator.clipboard.writeText(toYaml(sheetCheck, sheetDetail));
    copied = true;
    setTimeout(() => copied = false, 2000);
  }

  $effect(() => { load(); });
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div class="space-y-1">
    <h1 class="text-xl font-semibold">Checks</h1>
    <p class="text-sm text-muted-foreground">All monitoring checks across every service.</p>
  </div>

  {#if error}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Description>{error}</Alert.Description>
    </Alert.Root>
  {/if}

  {#if !loading}
    <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
      <Card.Root class="p-4">
        <p class="text-xs text-muted-foreground">Total</p>
        <p class="text-2xl font-semibold">{stats.total}</p>
      </Card.Root>
      <Card.Root class="p-4">
        <p class="text-xs text-muted-foreground">Up</p>
        <p class="text-2xl font-semibold text-green-600 dark:text-green-400">{stats.up}</p>
      </Card.Root>
      <Card.Root class="p-4">
        <p class="text-xs text-muted-foreground">Degraded</p>
        <p class="text-2xl font-semibold text-yellow-600 dark:text-yellow-400">{stats.degraded}</p>
      </Card.Root>
      <Card.Root class="p-4">
        <p class="text-xs text-muted-foreground">Down</p>
        <p class="text-2xl font-semibold text-red-600 dark:text-red-400">{stats.down}</p>
      </Card.Root>
    </div>
  {/if}

  <Card.Root>
    <Card.Header class="pb-3">
      <div class="relative">
        <SearchIcon class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
        <Input placeholder="Search checks, services, types…" bind:value={search} class="pl-9" />
      </div>
    </Card.Header>

    {#if loading}
      <Card.Content class="flex justify-center py-12">
        <Spinner class="size-5 text-muted-foreground" />
      </Card.Content>
    {:else if filtered.length === 0}
      <Card.Content class="py-12 text-center text-sm text-muted-foreground">
        {search ? "No checks match your search." : "No checks configured yet."}
      </Card.Content>
    {:else}
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b bg-muted/40 text-left">
              <th class="px-4 py-2.5 font-medium text-muted-foreground">Status</th>
              <th class="px-4 py-2.5 font-medium text-muted-foreground">Check</th>
              <th class="px-4 py-2.5 font-medium text-muted-foreground">Service</th>
              <th class="px-4 py-2.5 font-medium text-muted-foreground">Type</th>
              <th class="px-4 py-2.5 font-medium text-muted-foreground">Cron</th>
              <th class="px-4 py-2.5 font-medium text-muted-foreground">Active</th>
              <th class="px-4 py-2.5"></th>
            </tr>
          </thead>
          <tbody class="divide-y">
            {#each filtered as check (check.id)}
              <tr class="hover:bg-muted/30 transition-colors {!check.isActive ? 'opacity-50' : ''}">
                <td class="px-4 py-3">
                  {#if check.lastErrorMessage && check.currentStatus !== "UP"}
                    <Tooltip.Root>
                      <Tooltip.Trigger>
                        <div class="flex items-center gap-2 cursor-help">
                          <span class="size-2 rounded-full shrink-0 {STATUS_DOT[check.currentStatus] ?? 'bg-muted-foreground'}"></span>
                          <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium {STATUS_COLOR[check.currentStatus] ?? ''}">
                            {formatStatus(check.currentStatus)}
                          </span>
                        </div>
                      </Tooltip.Trigger>
                      <Tooltip.Content class="max-w-sm text-xs whitespace-pre-wrap break-words">{check.lastErrorMessage}</Tooltip.Content>
                    </Tooltip.Root>
                  {:else}
                    <div class="flex items-center gap-2">
                      <span class="size-2 rounded-full shrink-0 {STATUS_DOT[check.currentStatus] ?? 'bg-muted-foreground'}"></span>
                      <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium {STATUS_COLOR[check.currentStatus] ?? ''}">
                        {formatStatus(check.currentStatus)}
                      </span>
                    </div>
                  {/if}
                </td>
                <td class="px-4 py-3">
                  <a href="/admin/services/{check.serviceSlug}/checks/{check.slug}" class="font-medium hover:underline">{check.name}</a>
                  {#if check.description}
                    <p class="text-xs text-muted-foreground truncate max-w-[200px]">{check.description}</p>
                  {/if}
                </td>
                <td class="px-4 py-3">
                  <a href="/admin/services/{check.serviceSlug}" class="text-muted-foreground hover:text-foreground hover:underline text-xs">
                    {check.serviceName}
                  </a>
                </td>
                <td class="px-4 py-3">
                  <Badge variant="outline" class="text-xs font-mono">{check.type}</Badge>
                </td>
                <td class="px-4 py-3 font-mono text-xs text-muted-foreground">{check.cron}</td>
                <td class="px-4 py-3">
                  <span class="text-xs {check.isActive ? 'text-green-600 dark:text-green-400' : 'text-muted-foreground'}">
                    {check.isActive ? "Yes" : "No"}
                  </span>
                </td>
                <td class="px-4 py-3">
                  <div class="flex items-center gap-1">
                    <Button variant="ghost" size="icon" class="size-7 text-muted-foreground" onclick={() => openYaml(check)}>
                      <FileCodeIcon class="size-4" />
                    </Button>
                    <Button variant="ghost" size="icon" class="size-7 text-muted-foreground" href="/admin/services/{check.serviceSlug}/checks/{check.slug}">
                      <SettingsIcon class="size-4" />
                    </Button>
                  </div>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    {/if}
  </Card.Root>
</div>

<!-- YAML side panel -->
<Sheet.Root bind:open={sheetOpen}>
  <Sheet.Content side="right" class="w-full sm:max-w-lg flex flex-col">
    <Sheet.Header>
      <Sheet.Title class="flex items-center justify-between pr-6">
        <span>{sheetCheck?.name ?? "Check"}</span>
        {#if sheetDetail && sheetCheck}
          <Button variant="ghost" size="icon" class="size-7" onclick={copyYaml}>
            {#if copied}<CheckIcon class="size-4 text-green-600" />{:else}<CopyIcon class="size-4" />{/if}
          </Button>
        {/if}
      </Sheet.Title>
      <Sheet.Description>
        YAML definition · <span class="font-mono">{sheetCheck?.serviceSlug}/{sheetCheck?.slug}</span>
      </Sheet.Description>
    </Sheet.Header>

    <div class="flex-1 overflow-auto mt-4 px-1">
      {#if sheetLoading}
        <!-- Skeleton -->
        <div class="space-y-2 animate-pulse">
          {#each [80, 60, 70, 50, 90, 55, 65] as w}
            <div class="h-4 rounded bg-muted" style="width: {w}%"></div>
          {/each}
        </div>
      {:else if highlightedHtml}
        <div class="shiki-wrap text-xs rounded-lg overflow-auto leading-relaxed [&_pre]:p-4 [&_pre]:!bg-transparent">{@html highlightedHtml}</div>
      {:else}
        <p class="text-sm text-muted-foreground text-center py-8">Failed to load check details.</p>
      {/if}
    </div>
  </Sheet.Content>
</Sheet.Root>
