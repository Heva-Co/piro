<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Label } from "$lib/components/ui/label/index.js";

  let webhookUrl = $state("");
  let username = $state("");
  let template = $state("");

  export function getMeta() {
    return { webhookUrl, username: username || null, template: template || null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      webhookUrl = m.webhookUrl ?? "";
      username = m.username ?? "";
      template = m.template ?? "";
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!webhookUrl.trim()) return "Webhook URL is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>Webhook URL <span class="text-destructive">*</span></Label>
  <Input bind:value={webhookUrl} placeholder="https://discord.com/api/webhooks/..." />
  <p class="text-xs text-muted-foreground">Create one under Server Settings → Integrations → Webhooks.</p>
</div>
<div class="space-y-1.5">
  <Label>Bot Username</Label>
  <Input bind:value={username} placeholder="Piro" />
  <p class="text-xs text-muted-foreground">Override the webhook display name (optional).</p>
</div>
<div class="space-y-1.5">
  <Label>Custom Message Template</Label>
  <p class="text-xs text-muted-foreground">
    Leave blank to use the default embed. Use Mustache variables:
    <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_description, alert_timestamp, is_resolved, is_triggered</span>
  </p>
  <Textarea bind:value={template} class="font-mono text-sm min-h-24"
    placeholder={"🚨 **{{alert_name}}** on {{alert_for}} is {{alert_status}}"} />
</div>
