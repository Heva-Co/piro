<script lang="ts">
  import * as Card from "$lib/components/ui/card/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { toast } from "svelte-sonner";
  import CopyIcon from "@lucide/svelte/icons/copy";
  import CheckIcon from "@lucide/svelte/icons/check";

  let enabled = $state<boolean | null>(null);
  let instanceId = $state("");
  let loading = $state(true);
  let saving = $state(false);
  let copied = $state(false);
  let error = $state("");

  async function api(action: string, body?: object) {
    const res = await fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action, ...body }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  }

  async function load() {
    try {
      const data = await api("getTelemetryConfig");
      enabled = data.enabled;
      instanceId = data.instanceId;
    } catch {
      error = "Failed to load telemetry configuration.";
    } finally {
      loading = false;
    }
  }

  async function toggle() {
    saving = true;
    try {
      await api("setTelemetryEnabled", { enabled: !enabled });
      enabled = !enabled;
      toast.success(enabled ? "Telemetry enabled." : "Telemetry disabled.");
    } catch {
      toast.error("Failed to update telemetry setting.");
    } finally {
      saving = false;
    }
  }

  async function copyInstanceId() {
    await navigator.clipboard.writeText(instanceId);
    copied = true;
    setTimeout(() => (copied = false), 2000);
  }

  load();
</script>

<div class="flex w-full flex-col gap-4 p-4">
  <div>
    <h1 class="text-2xl font-bold">Telemetry</h1>
    <p class="text-muted-foreground text-sm mt-1">
      Configure anonymized usage reporting for Piro.
    </p>
  </div>

  {#if loading}
    <div class="flex justify-center py-10"><Spinner class="size-6" /></div>
  {:else if error}
    <p class="text-sm text-destructive">{error}</p>
  {:else}
    <Card.Root>
      <Card.Header>
        <Card.Title>Usage Telemetry</Card.Title>
        <Card.Description>
          Sends anonymized events (service counts, check types, incident resolution times) to PostHog.
          No hostnames, URLs, credentials, or personally identifiable information are ever transmitted.
        </Card.Description>
      </Card.Header>
      <Card.Content class="flex items-center justify-between gap-4">
        <div class="text-sm">
          Status: <span class="font-medium">{enabled ? "Enabled" : "Disabled"}</span>
        </div>
        <Button variant={enabled ? "outline" : "default"} size="sm" onclick={toggle} disabled={saving}>
          {#if saving}<Spinner class="size-4 mr-2" />{/if}
          {enabled ? "Disable" : "Enable"}
        </Button>
      </Card.Content>
    </Card.Root>

    <Card.Root>
      <Card.Header>
        <Card.Title>Instance ID</Card.Title>
        <Card.Description>
          A randomly generated identifier used to correlate anonymous events. It contains no personal or infrastructure information.
        </Card.Description>
      </Card.Header>
      <Card.Content>
        <div class="flex items-center gap-2">
          <code class="flex-1 rounded-md bg-muted px-3 py-2 text-sm font-mono break-all">{instanceId}</code>
          <Button variant="outline" size="icon" onclick={copyInstanceId} aria-label="Copy instance ID">
            {#if copied}
              <CheckIcon class="size-4" />
            {:else}
              <CopyIcon class="size-4" />
            {/if}
          </Button>
        </div>
      </Card.Content>
    </Card.Root>

    <Card.Root>
      <Card.Header>
        <Card.Title>Error Tracking (Sentry)</Card.Title>
        <Card.Description>
          Backend and frontend error tracking via Sentry is configured through environment variables, not the admin UI.
          Set <code class="rounded bg-muted px-1 py-0.5 text-xs">Sentry__Dsn</code> on the API and
          <code class="rounded bg-muted px-1 py-0.5 text-xs">PUBLIC_SENTRY_DSN</code> on the frontend to enable it.
        </Card.Description>
      </Card.Header>
    </Card.Root>
  {/if}
</div>
