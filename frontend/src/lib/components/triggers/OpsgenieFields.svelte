<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import * as Select from "$lib/components/ui/select/index.js";

  let apiKey = $state("");
  let region = $state("us");
  let priority = $state("");

  export function getMeta() {
    return { apiKey, region: region || null, priority: priority || null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      apiKey = m.apiKey ?? "";
      region = m.region ?? "us";
      priority = m.priority ?? "";
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!apiKey.trim()) return "API Key is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>API Key <span class="text-destructive">*</span></Label>
  <Input bind:value={apiKey} type="password" placeholder="Your Opsgenie API integration key" />
  <p class="text-xs text-muted-foreground">Found under Teams → Integrations → API in Opsgenie.</p>
</div>
<div class="space-y-1.5">
  <Label>Region</Label>
  <Select.Root type="single" bind:value={region}>
    <Select.Trigger class="w-full">
      {region === "eu" ? "EU (api.eu.opsgenie.com)" : "US (api.opsgenie.com)"}
    </Select.Trigger>
    <Select.Content>
      <Select.Item value="us">US (api.opsgenie.com)</Select.Item>
      <Select.Item value="eu">EU (api.eu.opsgenie.com)</Select.Item>
    </Select.Content>
  </Select.Root>
</div>
<div class="space-y-1.5">
  <Label>Priority</Label>
  <Select.Root type="single" bind:value={priority}>
    <Select.Trigger class="w-full">
      {priority || "Auto (based on severity)"}
    </Select.Trigger>
    <Select.Content>
      <Select.Item value="">Auto (based on severity)</Select.Item>
      <Select.Item value="P1">P1 — Critical</Select.Item>
      <Select.Item value="P2">P2 — High</Select.Item>
      <Select.Item value="P3">P3 — Moderate</Select.Item>
      <Select.Item value="P4">P4 — Low</Select.Item>
      <Select.Item value="P5">P5 — Informational</Select.Item>
    </Select.Content>
  </Select.Root>
</div>
