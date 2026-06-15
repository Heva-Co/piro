<script lang="ts">
  import type { PageData } from "./$types";
  import type { ServiceStatus } from "$lib/api";
  import MonitorIcon from "lucide-svelte/icons/monitor";

  let { data }: { data: PageData } = $props();

  const { incident } = data;

  const stateColor: Record<string, string> = {
    Investigating: "text-amber-500",
    Identified:    "text-orange-500",
    Monitoring:    "text-blue-500",
    Resolved:      "text-green-500",
  };

  const impactColor: Record<ServiceStatus, string> = {
    DOWN:        "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400",
    DEGRADED:    "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400",
    MAINTENANCE: "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400",
    UP:          "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400",
    NO_DATA:     "bg-secondary text-muted-foreground",
  };

  function fmtTs(ts: number): string {
    return new Date(ts * 1000).toLocaleString(undefined, {
      month: "short", day: "numeric", year: "numeric",
      hour: "numeric", minute: "2-digit",
    });
  }
</script>

<svelte:head>
  <title>{incident.title}</title>
</svelte:head>

<div class="max-w-6xl mx-auto px-4 py-10 flex flex-col gap-6">
  <!-- Title -->
  <div>
    <a href="/" class="text-sm text-muted-foreground hover:underline mb-2 inline-block">← Back</a>
    <h1 class="text-3xl font-bold">{incident.title}</h1>
    <p class="text-muted-foreground text-sm mt-1">
      Started {fmtTs(incident.startDateTime)}
      {#if incident.endDateTime}
        · Resolved {fmtTs(incident.endDateTime)}
      {:else}
        · <span class="{stateColor[incident.state] ?? ''}">{incident.state}</span>
      {/if}
    </p>
  </div>

  <!-- Two-column layout -->
  <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
    <!-- Updates (left, 2/3) -->
    <div class="lg:col-span-2 rounded-2xl border overflow-hidden">
      <div class="flex items-center gap-2 px-5 py-4 border-b bg-secondary/30">
        <MonitorIcon class="size-4 text-muted-foreground" />
        <span class="font-medium text-sm">Updates ({incident.comments.length})</span>
      </div>
      {#if incident.comments.length === 0}
        <div class="px-5 py-8 text-center text-muted-foreground text-sm">No updates yet.</div>
      {:else}
        <div class="divide-y">
          {#each incident.comments as comment (comment.id)}
            <div class="px-5 py-4 flex flex-col gap-1">
              <div class="flex items-center justify-between gap-2">
                <span class="text-xs font-semibold uppercase tracking-wide {stateColor[comment.state] ?? 'text-muted-foreground'}">
                  {comment.state}
                </span>
                <span class="text-xs text-muted-foreground">{fmtTs(comment.commentedAt)}</span>
              </div>
              <p class="text-sm">{comment.comment}</p>
            </div>
          {/each}
        </div>
      {/if}
    </div>

    <!-- Affected Services (right, 1/3) -->
    <div class="rounded-2xl border overflow-hidden">
      <div class="flex items-center gap-2 px-5 py-4 border-b bg-secondary/30">
        <MonitorIcon class="size-4 text-muted-foreground" />
        <span class="font-medium text-sm">
          Affected Services ({incident.isGlobal ? "All" : incident.services.length})
        </span>
      </div>
      {#if incident.isGlobal}
        <div class="px-5 py-6 flex flex-col items-center gap-2 text-center">
          <MonitorIcon class="size-10 text-amber-500 opacity-70" />
          <span class="text-sm font-medium">All services affected</span>
          <span class="text-xs text-muted-foreground">This is a global incident affecting all services.</span>
        </div>
      {:else if incident.services.length === 0}
        <div class="px-5 py-10 flex flex-col items-center gap-2 text-muted-foreground">
          <MonitorIcon class="size-10 opacity-30" />
          <span class="text-sm">No services affected</span>
        </div>
      {:else}
        <div class="divide-y">
          {#each incident.services as svc (svc.serviceSlug)}
            <div class="px-5 py-3 flex items-center justify-between gap-3">
              <a
                href="/services/{svc.serviceSlug}"
                class="text-sm font-medium hover:underline"
              >
                {svc.serviceSlug}
              </a>
              <span class="text-xs font-semibold rounded-full px-2.5 py-0.5 {impactColor[svc.impact]}">
                {svc.impact}
              </span>
            </div>
          {/each}
        </div>
      {/if}
    </div>
  </div>
</div>
