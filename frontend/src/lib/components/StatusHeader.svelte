<script lang="ts">
  import type { ServiceStatus } from "$lib/api";

  let { status, text }: { status: ServiceStatus; text: string } = $props();

  const pingColor: Record<ServiceStatus, string> = {
    UP:          "bg-green-500",
    DEGRADED:    "bg-amber-500",
    DOWN:        "bg-red-500",
    MAINTENANCE: "bg-indigo-500",
    NO_DATA:     "bg-gray-400",
  };
</script>

<div class="rounded-3xl border px-5 py-5 flex items-center gap-4">
  <span class="relative flex size-4 shrink-0">
    <span class="absolute inline-flex h-full w-full animate-ping rounded-full opacity-75 {pingColor[status]}"></span>
    <span class="relative inline-flex size-4 rounded-full {pingColor[status]}"></span>
  </span>
  <span class="text-xl sm:text-2xl font-medium">{text}</span>
</div>
