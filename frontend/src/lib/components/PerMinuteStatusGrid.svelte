<script lang="ts">
  import type { StatusPoint } from "$lib/api.js";

  interface Props {
    history: StatusPoint[];
    uptimePercent?: number | null;
  }

  let { history, uptimePercent = null }: Props = $props();

  const minuteMap = $derived.by(() => {
    const m = new Map<number, string>();
    for (const p of history) {
      const d = new Date(p.timestamp * 1000);
      const minuteOfDay = d.getUTCHours() * 60 + d.getUTCMinutes();
      m.set(minuteOfDay, p.status);
    }
    return m;
  });

  const ROWS = [
    { label: "00:00 - 05:59", start: 0 },
    { label: "06:00 - 11:59", start: 360 },
    { label: "12:00 - 17:59", start: 720 },
    { label: "18:00 - 23:59", start: 1080 },
  ];

  const cellColor: Record<string, string> = {
    UP:          "bg-green-500",
    DEGRADED:    "bg-yellow-400",
    DOWN:        "bg-red-500",
    MAINTENANCE: "bg-blue-500",
  };

  function getColor(minuteOfDay: number): string {
    const status = minuteMap.get(minuteOfDay);
    return status ? (cellColor[status] ?? "bg-muted") : "bg-muted";
  }

  const statusColor: Record<string, string> = {
    UP:          "text-green-500",
    DEGRADED:    "text-yellow-500",
    DOWN:        "text-red-500",
    MAINTENANCE: "text-blue-500",
    NO_DATA:     "text-muted-foreground",
  };

  // Tooltip
  let tooltipVisible = $state(false);
  let tooltipX = $state(0);
  let tooltipY = $state(0);
  let tooltipText = $state("");
  let tooltipStatus = $state("NO_DATA");

  function formatMinute(minuteOfDay: number): string {
    const h = minuteOfDay / 60;
    const m = minuteOfDay % 60;
    const ampm = h >= 12 ? "PM" : "AM";
    const hour = ((Math.floor(h) + 11) % 12) + 1;
    return `${hour}:${m.toString().padStart(2, "0")} ${ampm}`;
  }

  function handleMouseEnter(event: MouseEvent, minuteOfDay: number) {
    tooltipStatus = minuteMap.get(minuteOfDay) ?? "NO_DATA";
    tooltipText = `${tooltipStatus} @ ${formatMinute(minuteOfDay)}`;
    updatePosition(event);
    tooltipVisible = true;
  }

  function handleMouseMove(event: MouseEvent) {
    updatePosition(event);
  }

  function handleMouseLeave() {
    tooltipVisible = false;
  }

  function updatePosition(event: MouseEvent) {
    tooltipX = event.clientX;
    tooltipY = event.clientY;
  }

  // Portal action — moves element to document.body so fixed positioning
  // is not clipped by dialog's CSS transform containing block.
  function portal(node: HTMLElement) {
    document.body.appendChild(node);
    return { destroy() { node.remove(); } };
  }
</script>

<div class="flex flex-col gap-3">
  <div class="flex items-center justify-between">
    <p class="text-sm font-semibold">Per-Minute Status</p>
    {#if uptimePercent !== null}
      <p class="text-sm font-medium">{uptimePercent.toFixed(4)}%</p>
    {/if}
  </div>

  {#each ROWS as row (row.label)}
    <div class="flex flex-col gap-1">
      <p class="text-xs text-muted-foreground">{row.label}</p>
      <div class="grid gap-px" style="grid-template-columns: repeat(60, minmax(0, 1fr));">
        {#each Array.from({ length: 360 }, (_, i) => row.start + i) as min (min)}
          <div
            class="aspect-square rounded-[1px] {getColor(min)}"
            onmouseenter={(e) => handleMouseEnter(e, min)}
            onmousemove={handleMouseMove}
            onmouseleave={handleMouseLeave}
          ></div>
        {/each}
      </div>
    </div>
  {/each}

  {#if history.length === 0}
    <p class="text-xs text-muted-foreground text-center py-4">No data for today yet</p>
  {/if}
</div>

{#if tooltipVisible}
  <div
    use:portal
    class="pointer-events-none fixed z-[9999] rounded-xl border bg-popover px-3 py-1.5 text-sm font-medium shadow-md -translate-x-1/2 -translate-y-full {statusColor[tooltipStatus]}"
    style="left: {tooltipX}px; top: {tooltipY - 8}px;"
  >
    {tooltipText}
  </div>
{/if}
