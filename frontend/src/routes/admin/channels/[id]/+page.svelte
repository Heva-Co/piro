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
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CheckCircleIcon from "@lucide/svelte/icons/check-circle";
  import FlaskConicalIcon from "@lucide/svelte/icons/flask-conical";

  import TelegramFields from "$lib/components/triggers/TelegramFields.svelte";
  import EmailFields from "$lib/components/triggers/EmailFields.svelte";
  import WebhookFields from "$lib/components/triggers/WebhookFields.svelte";
  import SlackFields from "$lib/components/triggers/SlackFields.svelte";
  import PagerDutyFields from "$lib/components/triggers/PagerDutyFields.svelte";
  import MsTeamsFields from "$lib/components/triggers/MsTeamsFields.svelte";
  import GoogleChatFields from "$lib/components/triggers/GoogleChatFields.svelte";
  import TwilioSmsFields from "$lib/components/triggers/TwilioSmsFields.svelte";
  import DiscordFields from "$lib/components/triggers/DiscordFields.svelte";
  import OpsgenieFields from "$lib/components/triggers/OpsgenieFields.svelte";
  import PushoverFields from "$lib/components/triggers/PushoverFields.svelte";
  import NtfyFields from "$lib/components/triggers/NtfyFields.svelte";

  const TRIGGER_TYPES = ["Webhook", "Email", "Slack", "PagerDuty", "MSTeams", "Telegram", "TwilioSms", "GoogleChat", "Discord", "Opsgenie", "Pushover", "Ntfy"];
  const TRIGGER_LABELS: Record<string, string> = { MSTeams: "Microsoft Teams", TwilioSms: "Twilio SMS", GoogleChat: "Google Chat", Ntfy: "ntfy" };

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

  // Trigger field component refs
  let webhookRef: WebhookFields;
  let emailRef: EmailFields;
  let slackRef: SlackFields;
  let pagerDutyRef: PagerDutyFields;
  let msTeamsRef: MsTeamsFields;
  let telegramRef: TelegramFields;
  let twilioSmsRef: TwilioSmsFields;
  let googleChatRef: GoogleChatFields;
  let discordRef: DiscordFields;
  let opsgenieRef: OpsgenieFields;
  let pushoverRef: PushoverFields;
  let ntfyRef: NtfyFields;

  type TriggerComponent = { getMeta(): object; loadMeta(json: string): void; validate(): string | null };

  function getActiveRef(): TriggerComponent | undefined {
    switch (triggerType) {
      case "Webhook":    return webhookRef;
      case "Email":      return emailRef;
      case "Slack":      return slackRef;
      case "PagerDuty":  return pagerDutyRef;
      case "MSTeams":    return msTeamsRef;
      case "Telegram":   return telegramRef;
      case "TwilioSms":  return twilioSmsRef;
      case "GoogleChat": return googleChatRef;
      case "Discord":    return discordRef;
      case "Opsgenie":   return opsgenieRef;
      case "Pushover":   return pushoverRef;
      case "Ntfy":       return ntfyRef;
    }
  }

  function buildMetaJson(): string {
    return JSON.stringify(getActiveRef()?.getMeta() ?? {});
  }

  function validate(): string | null {
    if (!name.trim()) return "Name is required.";
    return getActiveRef()?.validate() ?? null;
  }

  async function load() {
    if (isNew) { loading = false; return; }
    loading = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getChannel", data: { id: parseInt(id) } }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      name = result.name;
      description = result.description ?? "";
      triggerType = result.type;
      isActive = result.status !== "INACTIVE";
      isGlobal = result.isGlobal ?? false;
      isLocked = result.isLocked ?? false;
      // defer loadMeta until the component is mounted
      await new Promise(r => setTimeout(r, 0));
      getActiveRef()?.loadMeta(result.metaJson ?? "{}");
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
      if (isNew) goto(`/admin/channels/${result.id}`);
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
        body: JSON.stringify({ action: "testChannel", data: {
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
        body: JSON.stringify({ action: "deleteChannel", data: { id: parseInt(id) } }),
      });
      goto("/admin/channels");
    } catch { error = "Failed to delete trigger."; }
  }

  onMount(load);
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <!-- Breadcrumb -->
  <div class="flex items-center gap-2 text-sm text-muted-foreground">
    <a href="/admin/channels" class="hover:text-foreground">Notification Channels</a>
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

        <!-- Per-type fields — all rendered, only active one visible -->
        <div class={triggerType !== "Webhook"    ? "hidden" : "contents"}><WebhookFields    bind:this={webhookRef}    /></div>
        <div class={triggerType !== "Email"      ? "hidden" : "contents"}><EmailFields      bind:this={emailRef}      /></div>
        <div class={triggerType !== "Slack"      ? "hidden" : "contents"}><SlackFields      bind:this={slackRef}      /></div>
        <div class={triggerType !== "PagerDuty"  ? "hidden" : "contents"}><PagerDutyFields  bind:this={pagerDutyRef}  /></div>
        <div class={triggerType !== "MSTeams"    ? "hidden" : "contents"}><MsTeamsFields    bind:this={msTeamsRef}    /></div>
        <div class={triggerType !== "Telegram"   ? "hidden" : "contents"}><TelegramFields   bind:this={telegramRef}   /></div>
        <div class={triggerType !== "TwilioSms"  ? "hidden" : "contents"}><TwilioSmsFields  bind:this={twilioSmsRef}  /></div>
        <div class={triggerType !== "GoogleChat" ? "hidden" : "contents"}><GoogleChatFields bind:this={googleChatRef} /></div>
        <div class={triggerType !== "Discord"    ? "hidden" : "contents"}><DiscordFields    bind:this={discordRef}    /></div>
        <div class={triggerType !== "Opsgenie"   ? "hidden" : "contents"}><OpsgenieFields   bind:this={opsgenieRef}   /></div>
        <div class={triggerType !== "Pushover"   ? "hidden" : "contents"}><PushoverFields   bind:this={pushoverRef}   /></div>
        <div class={triggerType !== "Ntfy"       ? "hidden" : "contents"}><NtfyFields       bind:this={ntfyRef}       /></div>

      </Card.Content>
      <Card.Footer class="flex justify-between gap-2">
        <Button variant="outline" onclick={testTrigger} disabled={testing || saving}>
          {#if testing}<Spinner class="size-4 mr-1" />{:else}<FlaskConicalIcon class="size-4 mr-1" />{/if}
          Test Trigger
        </Button>
        <div class="flex gap-2">
          <Button variant="outline" onclick={() => goto("/admin/channels")}>Cancel</Button>
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
