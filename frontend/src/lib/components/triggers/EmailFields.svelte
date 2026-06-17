<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import DEFAULT_EMAIL_TEMPLATE from "$lib/templates/default-email.html?raw";

  let to = $state("");
  let from = $state("");
  let template = $state(DEFAULT_EMAIL_TEMPLATE);

  const PREVIEW_VARS: Record<string, string> = {
    alert_id:                "42",
    alert_name:              "API Health Check",
    alert_for:               "Production API",
    alert_status:            "Down",
    alert_severity:          "Critical",
    alert_description:       "HTTP 503 — Service Unavailable",
    alert_message:           "HTTP 503 — Service Unavailable",
    alert_timestamp:         new Date().toISOString(),
    alert_value:             "503",
    alert_failure_threshold: "3",
    alert_success_threshold: "2",
    alert_incident_url:      "",
    alert_cta_url:           "https://status.example.com",
    alert_cta_text:          "View Incident",
    is_resolved:             "false",
    is_triggered:            "true",
    site_url:                "https://status.example.com",
    site_name:               "Piro",
    site_logo_url:           "",
    colors_down:             "#dc2626",
    colors_up:               "#16a34a",
  };

  function renderPreview(tmpl: string): string {
    if (!tmpl.trim()) return "<p style='color:#666;text-align:center;padding:20px'>No template set</p>";
    let out = tmpl;
    out = out.replace(/\{\{#(\w+)\}\}([\s\S]*?)\{\{\/\1\}\}/g, (_, key, content) => {
      const val = PREVIEW_VARS[key] ?? "";
      return val && val !== "false" ? content : "";
    });
    out = out.replace(/\{\{\{(\w+)\}\}\}/g, (_, key) => PREVIEW_VARS[key] ?? "");
    out = out.replace(/\{\{(\w+)\}\}/g, (_, key) => {
      const val = PREVIEW_VARS[key] ?? "";
      return val.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;");
    });
    return out;
  }

  export function getMeta() {
    return { to, from: from || null, template: template || null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      to = m.to ?? "";
      from = m.from ?? "";
      template = m.template ?? DEFAULT_EMAIL_TEMPLATE;
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!to.trim()) return "Recipient email is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>To <span class="text-destructive">*</span></Label>
  <Input bind:value={to} type="email" placeholder="alerts@example.com" />
  <p class="text-xs text-muted-foreground">Comma-separated for multiple recipients</p>
</div>
<div class="space-y-1.5">
  <Label>From</Label>
  <Input bind:value={from} placeholder="Piro Alerts <alerts@yourdomain.com>" />
</div>
<div class="space-y-1.5">
  <div class="flex items-center justify-between">
    <Label>Custom HTML Template</Label>
    <Button variant="ghost" size="sm" class="text-xs h-7 px-2" onclick={() => template = DEFAULT_EMAIL_TEMPLATE}>
      Reset to default
    </Button>
  </div>
  <p class="text-xs text-muted-foreground">
    Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
    Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_timestamp, is_resolved, is_triggered</span>
  </p>
  <Textarea bind:value={template} class="font-mono text-sm min-h-40"
    placeholder={"<h1>Alert: {{alert_name}}</h1>"} />
</div>
<div class="space-y-1.5">
  <p class="text-sm font-medium">Preview</p>
  <p class="text-xs text-muted-foreground">Sample render with placeholder values</p>
  <div class="rounded-lg border overflow-hidden bg-[#f4f4f4]">
    <iframe
      title="Email preview"
      class="w-full min-h-[520px] border-0"
      srcdoc={renderPreview(template)}
      sandbox="allow-same-origin"
    ></iframe>
  </div>
</div>
