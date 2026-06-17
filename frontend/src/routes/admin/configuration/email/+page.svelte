<script lang="ts">
  import { onMount } from "svelte";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CheckCircleIcon from "@lucide/svelte/icons/check-circle";
  import FlaskConicalIcon from "@lucide/svelte/icons/flask-conical";

  let loading = $state(true);
  let saving  = $state(false);
  let testing = $state(false);
  let error   = $state("");
  let success = $state("");

  let provider    = $state<"smtp" | "resend">("smtp");
  let smtpHost    = $state("");
  let smtpPort    = $state<number | "">(587);
  let smtpUsername = $state("");
  let smtpPassword = $state("");
  let smtpFrom    = $state("");
  let smtpUseTls  = $state(true);
  let resendApiKey = $state("");
  let resendFrom  = $state("");

  let hasSmtpPassword  = $state(false);
  let hasResendApiKey  = $state(false);

  async function load() {
    try {
      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getEmailConfig" }),
      });
      const data = await res.json();
      // Treat "Not Found" as no config saved yet — show empty form silently
      if (data.error) { if (data.error !== "Not Found") error = data.error; return; }
      provider     = data.provider ?? "smtp";
      smtpHost     = data.smtpHost ?? "";
      smtpPort     = data.smtpPort ?? 587;
      smtpUsername = data.smtpUsername ?? "";
      smtpFrom     = data.smtpFrom ?? "";
      smtpUseTls   = data.smtpUseTls ?? true;
      resendFrom   = data.resendFrom ?? "";
      hasSmtpPassword = data.hasSmtpPassword ?? false;
      hasResendApiKey = data.hasResendApiKey ?? false;
    } catch { error = "Failed to load email configuration."; }
    finally { loading = false; }
  }

  async function save() {
    saving = true; error = ""; success = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "updateEmailConfig", data: {
          provider,
          smtpHost: smtpHost || null,
          smtpPort: smtpPort !== "" ? Number(smtpPort) : null,
          smtpUsername: smtpUsername || null,
          smtpPassword: smtpPassword || null,
          smtpFrom: smtpFrom || null,
          smtpUseTls,
          resendApiKey: resendApiKey || null,
          resendFrom: resendFrom || null,
        }}),
      });
      const data = await res.json();
      if (data.error) { error = data.error; return; }
      success = "Email configuration saved.";
      // Clear secret fields after save — they're already stored
      smtpPassword = "";
      resendApiKey = "";
      await load();
    } catch { error = "Failed to save configuration."; }
    finally { saving = false; }
  }

  async function test() {
    testing = true; error = ""; success = "";
    try {
      // Save current config first so the test uses the provider shown on screen
      const saveRes = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "updateEmailConfig", data: {
          provider,
          smtpHost: smtpHost || null,
          smtpPort: smtpPort !== "" ? Number(smtpPort) : null,
          smtpUsername: smtpUsername || null,
          smtpPassword: smtpPassword || null,
          smtpFrom: smtpFrom || null,
          smtpUseTls,
          resendApiKey: resendApiKey || null,
          resendFrom: resendFrom || null,
        }}),
      });
      const saved = await saveRes.json();
      if (saved.error) { error = saved.error; return; }

      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "testEmailConfig" }),
      });
      const data = await res.json();
      if (data.error) error = data.error;
      else success = data.message ?? "Test email sent.";
    } catch { error = "Failed to send test email."; }
    finally { testing = false; }
  }

  onMount(load);
</script>

<div class="flex w-full flex-col gap-4 p-4 max-w-2xl">
  <div class="space-y-1">
    <h1 class="text-xl font-semibold">Email</h1>
    <p class="text-sm text-muted-foreground">Configure the email provider used for notifications and invitations</p>
  </div>

  {#if loading}
    <div class="flex justify-center py-16"><Spinner class="size-6" /></div>
  {:else}
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

        <div class="space-y-1.5">
          <Label>Provider</Label>
          <Select.Root type="single" bind:value={provider}>
            <Select.Trigger class="w-48">
              {provider === "resend" ? "Resend" : "SMTP"}
            </Select.Trigger>
            <Select.Content>
              <Select.Item value="smtp">SMTP</Select.Item>
              <Select.Item value="resend">Resend</Select.Item>
            </Select.Content>
          </Select.Root>
          <p class="text-xs text-muted-foreground">
            If no configuration is saved here, the app falls back to <span class="font-mono">Email:*</span> environment variables.
          </p>
        </div>

        {#if provider === "smtp"}
          <div class="grid grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <Label>Host <span class="text-destructive">*</span></Label>
              <Input bind:value={smtpHost} placeholder="smtp.example.com" />
            </div>
            <div class="space-y-1.5">
              <Label>Port</Label>
              <Input bind:value={smtpPort} type="number" placeholder="587" />
            </div>
          </div>
          <div class="space-y-1.5">
            <Label>Username</Label>
            <Input bind:value={smtpUsername} placeholder="user@example.com" autocomplete="off" />
          </div>
          <div class="space-y-1.5">
            <Label>Password</Label>
            <Input bind:value={smtpPassword} type="password" placeholder={hasSmtpPassword ? "••••••••  (saved — leave blank to keep)" : "SMTP password"} autocomplete="new-password" />
          </div>
          <div class="space-y-1.5">
            <Label>From address</Label>
            <Input bind:value={smtpFrom} placeholder="Piro <no-reply@example.com>" />
          </div>
          <div class="flex items-center justify-between rounded-lg border p-4">
            <div class="space-y-0.5">
              <p class="font-medium text-sm">Use TLS</p>
              <p class="text-xs text-muted-foreground">Enable SSL/TLS (port 465) or STARTTLS (port 587)</p>
            </div>
            <input type="checkbox" bind:checked={smtpUseTls} class="size-4 cursor-pointer" />
          </div>
        {/if}

        {#if provider === "resend"}
          <div class="space-y-1.5">
            <Label>API Key <span class="text-destructive">*</span></Label>
            <Input bind:value={resendApiKey} type="password" placeholder={hasResendApiKey ? "••••••••  (saved — leave blank to keep)" : "re_..."} autocomplete="new-password" />
            <p class="text-xs text-muted-foreground">Found in your Resend dashboard under API Keys.</p>
          </div>
          <div class="space-y-1.5">
            <Label>From address <span class="text-destructive">*</span></Label>
            <Input bind:value={resendFrom} placeholder="Piro <no-reply@yourdomain.com>" />
            <p class="text-xs text-muted-foreground">Must be a verified domain in Resend.</p>
          </div>
        {/if}

      </Card.Content>
      <Card.Footer class="flex justify-between gap-2">
        <Button variant="outline" onclick={test} disabled={testing || saving}>
          {#if testing}<Spinner class="size-4 mr-1" />{:else}<FlaskConicalIcon class="size-4 mr-1" />{/if}
          Send Test Email
        </Button>
        <Button onclick={save} disabled={saving || testing}>
          {#if saving}<Spinner class="size-4 mr-1" />{/if}
          Save
        </Button>
      </Card.Footer>
    </Card.Root>
  {/if}
</div>
