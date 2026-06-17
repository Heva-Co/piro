<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";

  let url = $state("");
  let routingKey = $state("");

  export function getMeta() {
    return { url, routingKey: routingKey || null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      url = m.url ?? "";
      routingKey = m.routingKey ?? "";
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!url.trim()) return "URL is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>Events API URL <span class="text-destructive">*</span></Label>
  <Input bind:value={url} placeholder="https://events.pagerduty.com/v2/enqueue" />
</div>
<div class="space-y-1.5">
  <Label>Routing Key</Label>
  <Input bind:value={routingKey} placeholder="PagerDuty Events API v2 routing key" />
</div>
