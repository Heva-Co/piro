<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { onMount } from "svelte";
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Switch from "$lib/components/ui/switch/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import Trash2Icon from "@lucide/svelte/icons/trash-2";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CheckCircleIcon from "@lucide/svelte/icons/check-circle";
  import FlaskConicalIcon from "@lucide/svelte/icons/flask-conical";

  import DEFAULT_EMAIL_TEMPLATE from "$lib/templates/default-email.html?raw";
  import DEFAULT_WEBHOOK_BODY from "$lib/templates/default-webhook.json?raw";
  import DEFAULT_SLACK_BODY from "$lib/templates/default-slack.json?raw";

  const TRIGGER_TYPES = ["Webhook", "Email", "Slack", "PagerDuty", "MSTeams", "Telegram", "TwilioSms", "GoogleChat"];
  const TRIGGER_LABELS: Record<string, string> = { MSTeams: "Microsoft Teams", TwilioSms: "Twilio SMS", GoogleChat: "Google Chat" };

  const id = $derived(page.params.id ?? "new");
  const isNew = $derived(id === "new");

  let loading = $state(true);
  let saving = $state(false);
  let testing = $state(false);
  let error = $state("");
  let success = $state("");
  let deleteConfirm = $state("");
  let showDelete = $state(false);

  // Form state
  let triggerType = $state("Webhook");
  let isActive = $state(true);
  let isGlobal = $state(false);
  let isLocked = $state(false);
  let name = $state("");
  let description = $state("");
  // Webhook / Slack / PagerDuty / MSTeams fields
  let url = $state("");
  let secret = $state("");
  let customBody = $state(DEFAULT_WEBHOOK_BODY);
  let headers = $state<{ key: string; value: string }[]>([]);
  // Email fields
  let emailTo = $state("");
  let emailFrom = $state("");
  let emailTemplate = $state("");
  // Telegram fields
  let telegramBotToken = $state("");
  let telegramChatId = $state("");
  let telegramTemplate = $state("");

  // Twilio SMS fields
  let twilioAccountSid = $state("");
  let twilioAuthToken = $state("");
  let twilioFromNumber = $state("");
  let twilioToNumber = $state("");

  $effect(() => {
    if (triggerType === "Webhook" && !customBody.trim()) customBody = DEFAULT_WEBHOOK_BODY;
    if (triggerType === "Slack" && !customBody.trim()) customBody = DEFAULT_SLACK_BODY;
    if (triggerType === "Email" && !emailTemplate.trim()) emailTemplate = DEFAULT_EMAIL_TEMPLATE;
  });

  const TELEGRAM_PREVIEW_VARS: Record<string, string> = {
    alert_name:        "API Health Check",
    alert_for:         "Production API",
    alert_status:      "Down",
    alert_severity:    "Critical",
    alert_description: "HTTP 503 — Service Unavailable",
    alert_timestamp:   new Date().toISOString(),
    is_resolved:       "false",
    is_triggered:      "true",
  };

  // Email preview
  const EMAIL_PREVIEW_VARS: Record<string, string> = {
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

  function renderEmailPreview(template: string): string {
    if (!template.trim()) return "<p style='color:#666;text-align:center;padding:20px'>No template set</p>";
    let out = template;
    // Sections: {{#var}}...{{/var}}
    out = out.replace(/\{\{#(\w+)\}\}([\s\S]*?)\{\{\/\1\}\}/g, (_, key, content) => {
      const val = EMAIL_PREVIEW_VARS[key] ?? "";
      return val && val !== "false" ? content : "";
    });
    // Triple braces — raw
    out = out.replace(/\{\{\{(\w+)\}\}\}/g, (_, key) => EMAIL_PREVIEW_VARS[key] ?? "");
    // Double braces — escaped
    out = out.replace(/\{\{(\w+)\}\}/g, (_, key) => {
      const val = EMAIL_PREVIEW_VARS[key] ?? "";
      return val.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;");
    });
    return out;
  }

  const DEFAULT_TELEGRAM_PREVIEW =
    "🚨 *CRITICAL* — Production API / API Health Check\n\nStatus: `Down`\nSeverity: Critical\nNote: HTTP 503 — Service Unavailable\nTime: " + new Date().toUTCString();

  function renderTelegramPreview(template: string): string {
    if (!template.trim()) return DEFAULT_TELEGRAM_PREVIEW;
    return template.replace(/\{\{(\w+)\}\}/g, (_, key) => TELEGRAM_PREVIEW_VARS[key] ?? `{{${key}}}`);
  }

  function telegramMdToHtml(text: string): string {
    // Telegram Markdown v1: *bold*, `code`, _italic_, escape special chars for display
    return text
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
      .replace(/\*([^*]+)\*/g, "<strong>$1</strong>")
      .replace(/`([^`]+)`/g, "<code class='bg-white/20 rounded px-0.5 font-mono text-xs'>$1</code>")
      .replace(/_([^_]+)_/g, "<em>$1</em>")
      .replace(/\n/g, "<br>");
  }

  function addHeader() {
    headers = [...headers, { key: "", value: "" }];
  }

  function removeHeader(i: number) {
    headers = headers.filter((_, idx) => idx !== i);
  }

  function buildMetaJson(): string {
    switch (triggerType) {
      case "Email":
        return JSON.stringify({ to: emailTo, from: emailFrom, template: emailTemplate || null });
      case "Webhook":
        return JSON.stringify({
          url,
          secret: secret || null,
          body: customBody || null,
          headers: headers.filter((h) => h.key.trim()),
        });
      case "Slack":
        return JSON.stringify({ url, body: customBody || null });
      case "PagerDuty":
        return JSON.stringify({ url, routingKey: secret || null });
      case "Telegram":
        return JSON.stringify({ botToken: telegramBotToken, chatId: telegramChatId, template: telegramTemplate || null });
      case "TwilioSms":
        return JSON.stringify({ accountSid: twilioAccountSid, authToken: twilioAuthToken, fromNumber: twilioFromNumber, toNumber: twilioToNumber });
      case "GoogleChat":
        return JSON.stringify({ webhookUrl: url, body: customBody || null });
      default:
        return JSON.stringify({ url });
    }
  }

  function loadMeta(metaJson: string) {
    try {
      const meta = JSON.parse(metaJson);
      url = meta.url ?? meta.webhookUrl ?? "";
      secret = meta.secret ?? meta.routingKey ?? "";
      emailTo = meta.to ?? "";
      emailFrom = meta.from ?? "";
      emailTemplate = meta.template ?? "";
      telegramBotToken = meta.botToken ?? "";
      telegramChatId = meta.chatId ?? "";
      telegramTemplate = meta.template ?? "";
      twilioAccountSid = meta.accountSid ?? "";
      twilioAuthToken = meta.authToken ?? "";
      twilioFromNumber = meta.fromNumber ?? "";
      twilioToNumber = meta.toNumber ?? "";
      headers = meta.headers ?? [];
      if (meta.body) customBody = meta.body;
    } catch { /* ignore */ }
  }

  function validate(): string | null {
    if (!name.trim()) return "Name is required.";
    if (triggerType === "Email" && !emailTo.trim()) return "Recipient email is required.";
    if (triggerType === "Telegram" && !telegramBotToken.trim()) return "Bot token is required.";
    if (triggerType === "Telegram" && !telegramChatId.trim()) return "Chat ID is required.";
    if (triggerType === "TwilioSms" && !twilioAccountSid.trim()) return "Account SID is required.";
    if (triggerType === "TwilioSms" && !twilioAuthToken.trim()) return "Auth Token is required.";
    if (triggerType === "TwilioSms" && !twilioFromNumber.trim()) return "From Number is required.";
    if (triggerType === "TwilioSms" && !twilioToNumber.trim()) return "To Number is required.";
    if (!["Email", "Telegram", "TwilioSms"].includes(triggerType) && !url.trim()) return "URL is required.";
    return null;
  }

  async function load() {
    if (isNew) { loading = false; return; }
    loading = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getTrigger", data: { id: parseInt(id) } }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      name = result.name;
      description = result.description ?? "";
      triggerType = result.type;
      isActive = result.status !== "INACTIVE";
      isGlobal = result.isGlobal ?? false;
      isLocked = result.isLocked ?? false;
      loadMeta(result.metaJson ?? "{}");
    } catch { error = "Failed to load trigger."; }
    finally { loading = false; }
  }

  async function save() {
    const err = validate();
    if (err) { error = err; return; }
    saving = true; error = ""; success = "";
    try {
      const payload = {
        name: name.trim(),
        type: triggerType,
        description: description.trim() || null,
        status: isActive ? "ACTIVE" : "INACTIVE",
        metaJson: buildMetaJson(),
        isGlobal,
        isLocked,
      };
      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: isNew ? "createTrigger" : "updateTrigger",
          data: isNew ? payload : { id: parseInt(id), ...payload },
        }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      if (isNew) goto(`/admin/triggers/${result.id}`);
      else success = "Trigger saved.";
    } catch { error = "Failed to save trigger."; }
    finally { saving = false; }
  }

  async function testTrigger() {
    const err = validate();
    if (err) { error = err; return; }
    testing = true; error = ""; success = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "testTrigger", data: {
          type: triggerType,
          name: name.trim() || "Test Trigger",
          metaJson: buildMetaJson(),
        }}),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else success = result.message ?? "Test notification sent successfully.";
    } catch { error = "Failed to send test notification."; }
    finally { testing = false; }
  }

  async function deleteTrigger() {
    if (deleteConfirm !== name) return;
    try {
      await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "deleteTrigger", data: { id: parseInt(id) } }),
      });
      goto("/admin/triggers");
    } catch { error = "Failed to delete trigger."; }
  }

  onMount(load);
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <!-- Breadcrumb -->
  <div class="flex items-center gap-2 text-sm text-muted-foreground">
    <a href="/admin/triggers" class="hover:text-foreground">Notification Channels</a>
    <span>/</span>
    <span class="text-foreground font-medium">{isNew ? "New Channel" : (name || id)}</span>
  </div>

  {#if loading}
    <div class="flex justify-center py-16"><Spinner class="size-6" /></div>
  {:else}
    <div class="space-y-1">
      <h1 class="text-xl font-semibold">{isNew ? "New Channel" : name}</h1>
      <p class="text-sm text-muted-foreground">Configure notification triggers for your monitors</p>
    </div>

    {#if error}
      <Alert.Root variant="destructive">
        <AlertCircleIcon />
        <Alert.Description>{error}</Alert.Description>
      </Alert.Root>
    {/if}
    {#if success}
      <Alert.Root class="border-green-200 bg-green-50 text-green-800 dark:bg-green-950 dark:border-green-800 dark:text-green-300">
        <CheckCircleIcon />
        <Alert.Description>{success}</Alert.Description>
      </Alert.Root>
    {/if}

    <Card.Root>
      <Card.Content class="space-y-6 pt-6">

        <!-- Trigger Type -->
        <div class="space-y-1.5">
          <Label>Trigger Type</Label>
          <p class="text-xs text-muted-foreground">Select the type of notification to send</p>
          <Select.Root type="single" value={triggerType} onValueChange={(v) => v && !isNew ? undefined : v && (triggerType = v)} disabled={!isNew}>
            <Select.Trigger class="w-64" disabled={!isNew}>{TRIGGER_LABELS[triggerType] ?? triggerType}</Select.Trigger>
            <Select.Content>
              {#each TRIGGER_TYPES as t (t)}<Select.Item value={t}>{TRIGGER_LABELS[t] ?? t}</Select.Item>{/each}
            </Select.Content>
          </Select.Root>
          {#if !isNew}
            <p class="text-xs text-muted-foreground">Trigger type cannot be changed after creation.</p>
          {/if}
        </div>

        <!-- Status -->
        <div class="flex items-center justify-between rounded-lg border p-4">
          <div class="space-y-0.5">
            <p class="font-medium text-sm">Status</p>
            <p class="text-xs text-muted-foreground">Enable or disable this trigger</p>
          </div>
          <Switch.Root bind:checked={isActive} />
        </div>

        <!-- Global -->
        <div class="flex items-center justify-between rounded-lg border p-4">
          <div class="space-y-0.5">
            <p class="font-medium text-sm">Global</p>
            <p class="text-xs text-muted-foreground">Automatically apply this trigger to all existing and future alert configs</p>
          </div>
          <Switch.Root bind:checked={isGlobal} />
        </div>

        <!-- Locked -->
        {#if isGlobal}
          <div class="flex items-center justify-between rounded-lg border p-4">
            <div class="space-y-0.5">
              <p class="font-medium text-sm">Locked</p>
              <p class="text-xs text-muted-foreground">Prevent users from removing this trigger from individual alert configs</p>
            </div>
            <Switch.Root bind:checked={isLocked} />
          </div>
        {/if}

        <!-- Name -->
        <div class="space-y-1.5">
          <Label>Name <span class="text-destructive">*</span></Label>
          <Input bind:value={name} placeholder="My Trigger" />
        </div>

        <!-- Description -->
        <div class="space-y-1.5">
          <Label>Description</Label>
          <Input bind:value={description} placeholder="Optional description" />
        </div>

        {#if triggerType === "Telegram"}
          <!-- Telegram fields -->
          <div class="space-y-1.5">
            <Label>Bot Token <span class="text-destructive">*</span></Label>
            <Input bind:value={telegramBotToken} placeholder="123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11" />
            <p class="text-xs text-muted-foreground">Get it from <span class="font-mono">@BotFather</span> on Telegram</p>
          </div>
          <div class="space-y-1.5">
            <Label>Chat ID <span class="text-destructive">*</span></Label>
            <Input bind:value={telegramChatId} placeholder="-1001234567890" />
            <p class="text-xs text-muted-foreground">User ID, group ID, or channel ID. Use <span class="font-mono">@userinfobot</span> to find yours.</p>
          </div>
          <div class="space-y-1.5">
            <Label>Custom Message Template</Label>
            <p class="text-xs text-muted-foreground">
              Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
              Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_description, alert_timestamp, is_resolved, is_triggered</span>
            </p>
            <Textarea bind:value={telegramTemplate} class="font-mono text-sm min-h-32"
              placeholder={"🚨 {{alert_name}} is {{alert_status}} for {{alert_for}}"} />
            <p class="text-xs text-muted-foreground">Leave empty to use the default message format. Supports Markdown.</p>
          </div>
          <!-- Telegram Preview -->
          <div class="space-y-1.5">
            <p class="text-sm font-medium">Preview</p>
            <p class="text-xs text-muted-foreground">Sample render with placeholder values</p>
            <div class="rounded-xl overflow-hidden" style="background-image: url('data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%2240%22 height=%2240%22%3E%3Crect width=%2240%22 height=%2240%22 fill=%22%23c8d8a0%22/%3E%3C/svg%3E'); background-color: #c8d8a0;">
              <div class="p-4 min-h-24 flex flex-col gap-2">
                <div class="max-w-xs self-start">
                  <div class="bg-white dark:bg-zinc-100 rounded-2xl rounded-tl-sm px-3 py-2 shadow-sm text-sm text-zinc-900 leading-relaxed">
                    {@html telegramMdToHtml(renderTelegramPreview(telegramTemplate))}
                  </div>
                  <p class="text-[10px] text-zinc-600 mt-0.5 pl-1">Piro Bot · just now</p>
                </div>
              </div>
            </div>
          </div>
        {:else if triggerType === "TwilioSms"}
          <!-- Twilio SMS fields -->
          <div class="space-y-1.5">
            <Label>Account SID <span class="text-destructive">*</span></Label>
            <Input bind:value={twilioAccountSid} placeholder="ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" />
            <p class="text-xs text-muted-foreground">Found in your <span class="font-mono">Twilio Console</span> dashboard.</p>
          </div>
          <div class="space-y-1.5">
            <Label>Auth Token <span class="text-destructive">*</span></Label>
            <Input bind:value={twilioAuthToken} type="password" placeholder="Your Twilio Auth Token" />
          </div>
          <div class="space-y-1.5">
            <Label>From Number <span class="text-destructive">*</span></Label>
            <Input bind:value={twilioFromNumber} placeholder="+15551234567" />
            <p class="text-xs text-muted-foreground">Your Twilio phone number in E.164 format.</p>
          </div>
          <div class="space-y-1.5">
            <Label>To Number <span class="text-destructive">*</span></Label>
            <Input bind:value={twilioToNumber} placeholder="+15559876543" />
            <p class="text-xs text-muted-foreground">Destination phone number in E.164 format.</p>
          </div>
          <div class="rounded-lg border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
            Messages are sent as a single SMS segment (≤160 chars) with the format:<br />
            <span class="font-mono text-xs">[Piro] CRITICAL: ServiceName/CheckName is Down. 2024-01-01 00:00:00Z</span>
          </div>
        {:else if triggerType === "Email"}
          <!-- Email fields -->
          <div class="space-y-1.5">
            <Label>To <span class="text-destructive">*</span></Label>
            <Input bind:value={emailTo} type="email" placeholder="alerts@example.com" />
            <p class="text-xs text-muted-foreground">Comma-separated for multiple recipients</p>
          </div>
          <div class="space-y-1.5">
            <Label>From</Label>
            <Input bind:value={emailFrom} placeholder="Piro Alerts <alerts@yourdomain.com>" />
          </div>
          <div class="space-y-1.5">
            <div class="flex items-center justify-between">
              <Label>Custom HTML Template</Label>
              <Button variant="ghost" size="sm" class="text-xs h-7 px-2" onclick={() => emailTemplate = DEFAULT_EMAIL_TEMPLATE}>
                Reset to default
              </Button>
            </div>
            <p class="text-xs text-muted-foreground">
              Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
              Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_timestamp, is_resolved, is_triggered</span>
            </p>
            <Textarea bind:value={emailTemplate} class="font-mono text-sm min-h-40"
              placeholder={"<h1>Alert: {{alert_name}}</h1>"} />
          </div>
          <!-- Email Preview -->
          <div class="space-y-1.5">
            <p class="text-sm font-medium">Preview</p>
            <p class="text-xs text-muted-foreground">Sample render with placeholder values</p>
            <div class="rounded-lg border overflow-hidden bg-[#f4f4f4]">
              <iframe
                title="Email preview"
                class="w-full min-h-[520px] border-0"
                srcdoc={renderEmailPreview(emailTemplate)}
                sandbox="allow-same-origin"
              ></iframe>
            </div>
          </div>
        {:else}
          <!-- URL -->
          <div class="space-y-1.5">
            <Label>URL <span class="text-destructive">*</span></Label>
            <Input bind:value={url} placeholder="https://example.com/webhook" />
            <p class="text-xs text-muted-foreground">The URL to send notifications to</p>
          </div>

          {#if triggerType === "Webhook"}
            <!-- Secret -->
            <div class="space-y-1.5">
              <Label>Secret</Label>
              <Input bind:value={secret} placeholder="Used to sign the payload via HMAC-SHA256 (optional)" />
            </div>

            <!-- Headers -->
            <div class="space-y-2">
              <Label>Headers</Label>
              {#each headers as h, i (i)}
                <div class="flex gap-2 items-center">
                  <Input bind:value={h.key} placeholder="Header name" class="flex-1" />
                  <Input bind:value={h.value} placeholder="Value" class="flex-1" />
                  <Button variant="ghost" size="icon" onclick={() => removeHeader(i)}>
                    <Trash2Icon class="size-4 text-destructive" />
                  </Button>
                </div>
              {/each}
              <Button variant="outline" size="sm" onclick={addHeader}>
                <PlusIcon class="size-4 mr-1" /> Add Header
              </Button>
            </div>

            <!-- Custom Body -->
            <div class="space-y-1.5">
              <Label>Custom Webhook Body</Label>
              <p class="text-xs text-muted-foreground">Override the default JSON payload</p>
              <p class="text-xs text-muted-foreground">
                Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
                Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_timestamp, is_resolved, is_triggered</span>
              </p>
              <Textarea bind:value={customBody} class="font-mono text-sm min-h-48" />
            </div>
          {:else if triggerType === "Slack"}
            <!-- Slack custom body -->
            <div class="space-y-1.5">
              <Label>Custom Slack Payload</Label>
              <p class="text-xs text-muted-foreground">
                Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
                Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, is_resolved</span>
              </p>
              <Textarea bind:value={customBody} class="font-mono text-sm min-h-48" />
            </div>
          {:else if triggerType === "GoogleChat"}
            <!-- Google Chat custom body -->
            <div class="space-y-1.5">
              <Label>Custom Card Payload</Label>
              <p class="text-xs text-muted-foreground">
                Override the default Google Chat card with a custom JSON payload.
                Use Mustache variables like <code class="bg-muted px-1 rounded text-xs">{"{{variable}}"}</code>.
                Available: <span class="font-mono text-xs">alert_name, alert_for, alert_status, alert_severity, alert_description, alert_timestamp, is_resolved, is_triggered</span>
              </p>
              <Textarea bind:value={customBody} class="font-mono text-sm min-h-48" />
            </div>
          {:else if triggerType === "PagerDuty"}
            <div class="space-y-1.5">
              <Label>Routing Key</Label>
              <Input bind:value={secret} placeholder="PagerDuty Events API v2 routing key" />
            </div>
          {/if}
        {/if}

      </Card.Content>
      <Card.Footer class="flex justify-between gap-2">
        <Button variant="outline" onclick={testTrigger} disabled={testing || saving}>
          {#if testing}<Spinner class="size-4 mr-1" />{:else}<FlaskConicalIcon class="size-4 mr-1" />{/if}
          Test Trigger
        </Button>
        <div class="flex gap-2">
          <Button variant="outline" onclick={() => goto("/admin/triggers")}>Cancel</Button>
          <Button onclick={save} disabled={saving || testing}>
            {#if saving}<Spinner class="size-4 mr-1" />{/if}
            {isNew ? "Create Trigger" : "Save Trigger"}
          </Button>
        </div>
      </Card.Footer>
    </Card.Root>

    <!-- Danger Zone (edit only) -->
    {#if !isNew}
      <Card.Root class="border-destructive/40">
        <Card.Header>
          <Card.Title class="text-destructive text-base">Danger Zone</Card.Title>
        </Card.Header>
        <Card.Content>
          {#if !showDelete}
            <div class="flex items-center justify-between">
              <div>
                <p class="text-sm font-medium">Delete this trigger</p>
                <p class="text-xs text-muted-foreground mt-0.5">Permanently removes this notification channel. Alert configs linked to it will stop working.</p>
              </div>
              <Button variant="destructive" size="sm" onclick={() => showDelete = true}>Delete</Button>
            </div>
          {:else}
            <div class="space-y-3">
              <p class="text-sm">Type <span class="font-mono font-medium">{name}</span> to confirm deletion:</p>
              <Input bind:value={deleteConfirm} placeholder={name} />
              <div class="flex gap-2">
                <Button variant="outline" size="sm" onclick={() => { showDelete = false; deleteConfirm = ""; }}>Cancel</Button>
                <Button variant="destructive" size="sm" disabled={deleteConfirm !== name} onclick={deleteTrigger}>
                  Confirm Delete
                </Button>
              </div>
            </div>
          {/if}
        </Card.Content>
      </Card.Root>
    {/if}
  {/if}
</div>
