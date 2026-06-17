<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import DEFAULT_SLACK_BODY from "$lib/templates/default-slack.json?raw";

  let url = $state("");
  let body = $state(DEFAULT_SLACK_BODY);

  export function getMeta() {
    return { url, body: body || null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      url = m.url ?? "";
      body = m.body ?? DEFAULT_SLACK_BODY;
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!url.trim()) return "URL is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>Webhook URL <span class="text-destructive">*</span></Label>
  <Input bind:value={url} placeholder="https://hooks.slack.com/services/..." />
</div>
<div class="space-y-1.5">
  <Label>Custom Slack Payload</Label>
  <p class="text-xs text-muted-foreground">
    Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
    Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, is_resolved</span>
  </p>
  <Textarea bind:value={body} class="font-mono text-sm min-h-48" />
</div>
