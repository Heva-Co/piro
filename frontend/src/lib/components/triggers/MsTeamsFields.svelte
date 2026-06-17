<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Label } from "$lib/components/ui/label/index.js";

  let url = $state("");
  let body = $state("");

  export function getMeta() {
    return { url, body: body || null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      url = m.url ?? "";
      body = m.body ?? "";
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!url.trim()) return "URL is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>Webhook URL <span class="text-destructive">*</span></Label>
  <Input bind:value={url} placeholder="https://xxx.webhook.office.com/webhookb2/..." />
  <p class="text-xs text-muted-foreground">Create one under Teams → Channel → Connectors → Incoming Webhook.</p>
</div>
<div class="space-y-1.5">
  <Label>Custom Adaptive Card Payload</Label>
  <p class="text-xs text-muted-foreground">
    Override the default card with a custom JSON payload.
    Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
    Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_description, alert_timestamp, is_resolved, is_triggered</span>
  </p>
  <Textarea bind:value={body} class="font-mono text-sm min-h-48" />
</div>
