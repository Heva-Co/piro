<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import * as Select from "$lib/components/ui/select/index.js";

  let topic = $state("");
  let serverUrl = $state("");
  let accessToken = $state("");
  let priority = $state("");

  export function getMeta() {
    return { topic, serverUrl: serverUrl || null, accessToken: accessToken || null, priority: priority !== "" ? parseInt(priority) : null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      topic = m.topic ?? "";
      serverUrl = m.serverUrl ?? "";
      accessToken = m.accessToken ?? "";
      priority = m.priority?.toString() ?? "";
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!topic.trim()) return "Topic is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>Topic <span class="text-destructive">*</span></Label>
  <Input bind:value={topic} placeholder="my-alerts" />
  <p class="text-xs text-muted-foreground">The topic name to publish to.</p>
</div>
<div class="space-y-1.5">
  <Label>Server URL</Label>
  <Input bind:value={serverUrl} placeholder="https://ntfy.sh" />
  <p class="text-xs text-muted-foreground">Leave blank to use <span class="font-mono">ntfy.sh</span>. Enter your self-hosted server URL if applicable.</p>
</div>
<div class="space-y-1.5">
  <Label>Access Token</Label>
  <Input bind:value={accessToken} type="password" placeholder="tk_..." />
  <p class="text-xs text-muted-foreground">Required for protected topics.</p>
</div>
<div class="space-y-1.5">
  <Label>Priority</Label>
  <Select.Root type="single" bind:value={priority}>
    <Select.Trigger class="w-full">
      {priority !== "" ? priority : "Auto (based on severity)"}
    </Select.Trigger>
    <Select.Content>
      <Select.Item value="">Auto (based on severity)</Select.Item>
      <Select.Item value="5">5 — Urgent</Select.Item>
      <Select.Item value="4">4 — High</Select.Item>
      <Select.Item value="3">3 — Default</Select.Item>
      <Select.Item value="2">2 — Low</Select.Item>
      <Select.Item value="1">1 — Min</Select.Item>
    </Select.Content>
  </Select.Root>
</div>
