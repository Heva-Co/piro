<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import Trash2Icon from "@lucide/svelte/icons/trash-2";
  import DEFAULT_WEBHOOK_BODY from "$lib/templates/default-webhook.json?raw";

  let url = $state("");
  let secret = $state("");
  let body = $state(DEFAULT_WEBHOOK_BODY);
  let headers = $state<{ key: string; value: string }[]>([]);

  export function getMeta() {
    return { url, secret: secret || null, body: body || null, headers: headers.filter(h => h.key.trim()) };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      url = m.url ?? "";
      secret = m.secret ?? "";
      body = m.body ?? DEFAULT_WEBHOOK_BODY;
      headers = m.headers ?? [];
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!url.trim()) return "URL is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>URL <span class="text-destructive">*</span></Label>
  <Input bind:value={url} placeholder="https://example.com/webhook" />
</div>
<div class="space-y-1.5">
  <Label>Secret</Label>
  <Input bind:value={secret} placeholder="Used to sign the payload via HMAC-SHA256 (optional)" />
</div>
<div class="space-y-2">
  <Label>Headers</Label>
  {#each headers as h, i (i)}
    <div class="flex gap-2 items-center">
      <Input bind:value={h.key} placeholder="Header name" class="flex-1" />
      <Input bind:value={h.value} placeholder="Value" class="flex-1" />
      <Button variant="ghost" size="icon" onclick={() => headers = headers.filter((_, idx) => idx !== i)}>
        <Trash2Icon class="size-4 text-destructive" />
      </Button>
    </div>
  {/each}
  <Button variant="outline" size="sm" onclick={() => headers = [...headers, { key: "", value: "" }]}>
    <PlusIcon class="size-4 mr-1" /> Add Header
  </Button>
</div>
<div class="space-y-1.5">
  <Label>Custom Webhook Body</Label>
  <p class="text-xs text-muted-foreground">Override the default JSON payload</p>
  <p class="text-xs text-muted-foreground">
    Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
    Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_timestamp, is_resolved, is_triggered</span>
  </p>
  <Textarea bind:value={body} class="font-mono text-sm min-h-48" />
</div>
