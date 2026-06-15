<script lang="ts">
  import type { ServiceStatus } from "$lib/api";

  let { status, size = "sm" }: { status: ServiceStatus; size?: "xs" | "sm" | "md" } = $props();

  const colorMap: Record<ServiceStatus, string> = {
    UP: "bg-green-500",
    DEGRADED: "bg-amber-500",
    DOWN: "bg-red-500",
    MAINTENANCE: "bg-indigo-500",
    NO_DATA: "bg-gray-400",
  };

  const sizeMap = { xs: "size-1.5", sm: "size-2", md: "size-2.5" };
</script>

<span
  title={status}
  class="rounded-full shrink-0 inline-block {sizeMap[size]} {colorMap[status]}"
></span>
