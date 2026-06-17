<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import * as Select from "$lib/components/ui/select/index.js";

  let appToken = $state("");
  let userKey = $state("");
  let priority = $state("");

  export function getMeta() {
    return { appToken, userKey, priority: priority !== "" ? parseInt(priority) : null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      appToken = m.appToken ?? "";
      userKey = m.userKey ?? "";
      priority = m.priority?.toString() ?? "";
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!appToken.trim()) return "App Token is required.";
    if (!userKey.trim()) return "User Key is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>App Token <span class="text-destructive">*</span></Label>
  <Input bind:value={appToken} type="password" placeholder="Your Pushover application token" />
  <p class="text-xs text-muted-foreground">Created at <span class="font-mono">pushover.net/apps/build</span>.</p>
</div>
<div class="space-y-1.5">
  <Label>User / Group Key <span class="text-destructive">*</span></Label>
  <Input bind:value={userKey} placeholder="Your Pushover user or group key" />
  <p class="text-xs text-muted-foreground">Found on your Pushover dashboard.</p>
</div>
<div class="space-y-1.5">
  <Label>Priority</Label>
  <Select.Root type="single" bind:value={priority}>
    <Select.Trigger class="w-full">
      {priority !== "" ? priority : "Auto (based on severity)"}
    </Select.Trigger>
    <Select.Content>
      <Select.Item value="">Auto (based on severity)</Select.Item>
      <Select.Item value="2">2 — Emergency (requires acknowledgement)</Select.Item>
      <Select.Item value="1">1 — High</Select.Item>
      <Select.Item value="0">0 — Normal</Select.Item>
      <Select.Item value="-1">-1 — Low</Select.Item>
      <Select.Item value="-2">-2 — Lowest (no notification)</Select.Item>
    </Select.Content>
  </Select.Root>
</div>
