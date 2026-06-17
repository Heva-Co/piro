<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Label } from "$lib/components/ui/label/index.js";

  let botToken = $state("");
  let chatId = $state("");
  let template = $state("");

  const PREVIEW_VARS: Record<string, string> = {
    alert_name:        "API Health Check",
    alert_for:         "Production API",
    alert_status:      "Down",
    alert_severity:    "Critical",
    alert_description: "HTTP 503 — Service Unavailable",
    alert_timestamp:   new Date().toISOString(),
    is_resolved:       "false",
    is_triggered:      "true",
  };

  const DEFAULT_PREVIEW =
    "🚨 *CRITICAL* — Production API / API Health Check\n\nStatus: `Down`\nSeverity: Critical\nNote: HTTP 503 — Service Unavailable\nTime: " + new Date().toUTCString();

  function renderPreview(tmpl: string): string {
    if (!tmpl.trim()) return DEFAULT_PREVIEW;
    return tmpl.replace(/\{\{(\w+)\}\}/g, (_, key) => PREVIEW_VARS[key] ?? `{{${key}}}`);
  }

  function mdToHtml(text: string): string {
    return text
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
      .replace(/\*([^*]+)\*/g, "<strong>$1</strong>")
      .replace(/`([^`]+)`/g, "<code class='bg-white/20 rounded px-0.5 font-mono text-xs'>$1</code>")
      .replace(/_([^_]+)_/g, "<em>$1</em>")
      .replace(/\n/g, "<br>");
  }

  export function getMeta() {
    return { botToken, chatId, template: template || null };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      botToken = m.botToken ?? "";
      chatId = m.chatId ?? "";
      template = m.template ?? "";
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!botToken.trim()) return "Bot token is required.";
    if (!chatId.trim()) return "Chat ID is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>Bot Token <span class="text-destructive">*</span></Label>
  <Input bind:value={botToken} placeholder="123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11" />
  <p class="text-xs text-muted-foreground">Get it from <span class="font-mono">@BotFather</span> on Telegram</p>
</div>
<div class="space-y-1.5">
  <Label>Chat ID <span class="text-destructive">*</span></Label>
  <Input bind:value={chatId} placeholder="-1001234567890" />
  <p class="text-xs text-muted-foreground">User ID, group ID, or channel ID. Use <span class="font-mono">@userinfobot</span> to find yours.</p>
</div>
<div class="space-y-1.5">
  <Label>Custom Message Template</Label>
  <p class="text-xs text-muted-foreground">
    Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
    Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_description, alert_timestamp, is_resolved, is_triggered</span>
  </p>
  <Textarea bind:value={template} class="font-mono text-sm min-h-32"
    placeholder={"🚨 {{alert_name}} is {{alert_status}} for {{alert_for}}"} />
  <p class="text-xs text-muted-foreground">Leave empty to use the default message format. Supports Markdown.</p>
</div>
<!-- Preview -->
<div class="space-y-1.5">
  <p class="text-sm font-medium">Preview</p>
  <p class="text-xs text-muted-foreground">Sample render with placeholder values</p>
  <div class="rounded-xl overflow-hidden" style="background-image: url('data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%2240%22 height=%2240%22%3E%3Crect width=%2240%22 height=%2240%22 fill=%22%23c8d8a0%22/%3E%3C/svg%3E'); background-color: #c8d8a0;">
    <div class="p-4 min-h-24 flex flex-col gap-2">
      <div class="max-w-xs self-start">
        <div class="bg-white dark:bg-zinc-100 rounded-2xl rounded-tl-sm px-3 py-2 shadow-sm text-sm text-zinc-900 leading-relaxed">
          {@html mdToHtml(renderPreview(template))}
        </div>
        <p class="text-[10px] text-zinc-600 mt-0.5 pl-1">Piro Bot · just now</p>
      </div>
    </div>
  </div>
</div>
