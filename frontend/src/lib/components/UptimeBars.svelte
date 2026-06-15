<script lang="ts">
  import type { StatusPoint, ServiceStatus } from "$lib/api";

  let { history }: { history: StatusPoint[] } = $props();

  // Bucket into hourly groups for display
  const BUCKETS = 168; // 7 days × 24 hours
  const now = Math.floor(Date.now() / 1000);
  const start = now - 86400 * 7;
  const bucketSize = (now - start) / BUCKETS;

  const colorMap: Record<ServiceStatus, string> = {
    UP: "bg-green-500",
    DEGRADED: "bg-amber-500",
    DOWN: "bg-red-500",
    MAINTENANCE: "bg-indigo-500",
    NO_DATA: "bg-gray-200 dark:bg-gray-700",
  };

  const bars = $derived.by(() => {
    const buckets: ServiceStatus[] = Array(BUCKETS).fill("NO_DATA");

    for (const point of history) {
      const idx = Math.floor((point.timestamp - start) / bucketSize);
      if (idx >= 0 && idx < BUCKETS) {
        // Worst-wins within a bucket
        const current = buckets[idx];
        const order: ServiceStatus[] = ["MAINTENANCE", "DOWN", "DEGRADED", "UP", "NO_DATA"];
        if (order.indexOf(point.status) < order.indexOf(current)) {
          buckets[idx] = point.status;
        }
      }
    }

    return buckets;
  });
</script>

<div class="flex gap-px items-end h-8" aria-label="Status history">
  {#each bars as status, i (i)}
    <div
      class="flex-1 rounded-sm h-full {colorMap[status]}"
      title={status}
    ></div>
  {/each}
</div>

<div class="flex justify-between mt-2 text-xs text-muted-foreground">
  <span>7 days ago</span>
  <span>Now</span>
</div>
