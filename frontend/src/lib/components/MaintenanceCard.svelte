<script lang="ts">
  import type { Maintenance } from "$lib/api";

  let { maintenance, upcoming = false }: { maintenance: Maintenance; upcoming?: boolean } = $props();

  const nextEvent = $derived(maintenance.upcomingEvents[0]);

  function fmtTs(ts: number) {
    return new Date(ts * 1000).toLocaleString(undefined, {
      month: "short", day: "numeric", year: "numeric",
      hour: "numeric", minute: "2-digit",
    });
  }

  function fmtDuration(seconds: number) {
    if (seconds < 60) return `${seconds} seconds`;
    const mins = Math.floor(seconds / 60);
    if (mins < 60) return `${mins} minute${mins !== 1 ? "s" : ""}`;
    const hrs = Math.floor(mins / 60);
    const remMins = mins % 60;
    if (remMins === 0) return `${hrs} hour${hrs !== 1 ? "s" : ""}`;
    return `${hrs}h ${remMins}m`;
  }

  const statusColor = upcoming ? "text-blue-500" : "text-blue-600";
  const statusLabel = upcoming ? "SCHEDULED" : "ONGOING";
</script>

<div class="rounded-3xl border p-5 flex flex-col gap-3">
  <!-- Status + Title -->
  <div class="flex flex-col gap-0.5">
    <span class="text-xs font-semibold uppercase tracking-wide {statusColor}">{statusLabel}</span>
    <h3 class="font-semibold text-base">{maintenance.title}</h3>
    {#if maintenance.description}
      <p class="text-sm text-muted-foreground">{maintenance.description}</p>
    {/if}
  </div>

  <!-- Timeline row -->
  {#if nextEvent}
    <div class="flex items-center justify-between gap-2 text-xs font-medium mt-1">
      <span class="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">{fmtTs(nextEvent.startDateTime)}</span>
      <span class="relative flex-1 text-center">
        <span class="absolute inset-y-1/2 left-0 right-0 border-t"></span>
        <span class="relative z-10 bg-background px-2 text-muted-foreground">
          {fmtDuration(maintenance.durationSeconds)}
        </span>
      </span>
      <span class="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">{fmtTs(nextEvent.endDateTime)}</span>
    </div>
  {/if}
</div>
