<script lang="ts">
  import type { Incident } from "$lib/api";
  import { slide } from "svelte/transition";
  import ChevronDownIcon from "lucide-svelte/icons/chevron-down";

  let { incident }: { incident: Incident } = $props();

  let showComments = $state(true);

  // State → color class
  const stateColor: Record<string, string> = {
    Investigating: "text-amber-500",
    Identified:    "text-orange-500",
    Monitoring:    "text-blue-500",
    Resolved:      "text-green-500",
  };

  function fmtTs(ts: number): string {
    return new Date(ts * 1000).toLocaleString(undefined, {
      month: "short", day: "numeric", year: "numeric",
      hour: "numeric", minute: "2-digit",
    });
  }

  function fmtDuration(from: number, to: number): string {
    const secs = to - from;
    if (secs < 60) return `${secs} seconds`;
    const mins = Math.floor(secs / 60);
    if (mins < 60) return `${mins} minute${mins !== 1 ? "s" : ""}`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs} hour${hrs !== 1 ? "s" : ""}`;
    const days = Math.floor(hrs / 24);
    return `${days} day${days !== 1 ? "s" : ""}`;
  }

  const endForDuration = $derived(incident.endDateTime ?? Math.floor(Date.now() / 1000));

  const lastUpdated = $derived(
    incident.comments.length > 0
      ? incident.comments[incident.comments.length - 1].commentedAt
      : incident.startDateTime
  );
</script>

<div class="rounded-3xl border p-4 flex flex-col gap-2">
  <!-- State + Title -->
  <div class="flex flex-col gap-0.5">
    <span class="text-xs font-semibold uppercase tracking-wide {stateColor[incident.state] ?? 'text-muted-foreground'}">
      {incident.state}
    </span>
    <a href="/incidents/{incident.id}" class="font-semibold text-base hover:underline">{incident.title}</a>
  </div>

  <!-- Timeline row -->
  <div class="flex items-center justify-between gap-2 text-xs font-medium mt-1">
    <span class="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">{fmtTs(incident.startDateTime)}</span>
    <span class="relative flex-1 text-center">
      <span class="absolute inset-y-1/2 left-0 right-0 border-t"></span>
      <span class="relative z-10 bg-background px-2 text-muted-foreground">
        {fmtDuration(incident.startDateTime, endForDuration)}
      </span>
    </span>
    {#if incident.endDateTime}
      <span class="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">{fmtTs(incident.endDateTime)}</span>
    {:else}
      <span class="shrink-0 rounded-full border px-3 py-1.5">Ongoing</span>
    {/if}
  </div>

  <!-- Summary row -->
  <div class="grid grid-cols-1 sm:grid-cols-3 gap-2 text-xs font-medium mt-1">
    <div class="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground">
      <span>Last Updated</span>
      <span>{fmtTs(lastUpdated)}</span>
    </div>
    <div class="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground">
      <span>Status</span>
      <span class="{stateColor[incident.state] ?? ''}">{incident.state}</span>
    </div>
    <div class="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground">
      <span>{incident.comments.length > 0 ? `${incident.comments.length} Update${incident.comments.length !== 1 ? "s" : ""}` : "No Updates"}</span>
      {#if incident.comments.length > 0}
        <button
          onclick={() => (showComments = !showComments)}
          class="rounded-full border bg-background p-1 hover:bg-muted transition-colors -mr-1"
        >
          <ChevronDownIcon class="size-3.5 transition-transform duration-200 {showComments ? 'rotate-180' : ''}" />
        </button>
      {/if}
    </div>
  </div>

  <!-- Comments -->
  {#if showComments && incident.comments.length > 0}
    <div transition:slide={{ duration: 200 }} class="flex flex-col gap-3 pt-2">
      {#each incident.comments as comment (comment.id)}
        <div class="flex flex-col gap-1 border-b pb-3 last:border-b-0 last:pb-0">
          <div class="flex items-center gap-2 text-xs">
            <span class="font-semibold uppercase tracking-wide {stateColor[comment.state] ?? 'text-muted-foreground'}">
              {comment.state}
            </span>
            <span class="text-muted-foreground">{fmtTs(comment.commentedAt)}</span>
          </div>
          <p class="text-sm">{comment.comment}</p>
        </div>
      {/each}
    </div>
  {/if}
</div>
