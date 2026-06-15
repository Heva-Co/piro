<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Field from "$lib/components/ui/field/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Switch from "$lib/components/ui/switch/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import BlendIcon from "@lucide/svelte/icons/blend";
  import { formatStatus } from "$lib/api.js";

  const serviceSlug = $derived(page.params.slug);

  const CHECK_TYPES = ["HTTP", "DNS", "TCP", "Ping", "SSL", "Heartbeat"];
  const HTTP_METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];
  const CRON_PRESETS = [
    { label: "Every minute",  value: "* * * * *" },
    { label: "Every 5 min",   value: "*/5 * * * *" },
    { label: "Every 15 min",  value: "*/15 * * * *" },
    { label: "Every hour",    value: "0 * * * *" },
    { label: "Every 6 hours", value: "0 */6 * * *" },
    { label: "Every day",     value: "0 0 * * *" },
  ];
  const DEFAULT_STATUSES = ["NO_DATA", "UP", "DOWN", "DEGRADED"];

  // General
  let name = $state("");
  let slug = $state("");
  let description = $state("");
  let checkType = $state("HTTP");
  let cron = $state("* * * * *");
  let customCron = $state(false);
  let customCronValue = $state("");
  let defaultStatus = $state("NO_DATA");
  let isActive = $state(true);
  let slugManuallyEdited = $state(false);
  let saving = $state(false);
  let error = $state("");

  // HTTP fields
  let httpUrl = $state("");
  let httpMethod = $state("GET");
  let httpTimeout = $state(5000);
  let httpExpectedCodes = $state("200");
  let httpBody = $state("");

  // DNS fields
  let dnsHost = $state("");
  let dnsNameServer = $state("8.8.8.8");

  // TCP fields
  let tcpHost = $state("");
  let tcpPort = $state(443);
  let tcpTimeout = $state(5000);

  // Ping fields
  let pingHost = $state("");
  let pingTimeout = $state(5000);

  // SSL fields
  let sslHost = $state("");
  let sslPort = $state(443);

  // Heartbeat fields
  let heartbeatGracePeriod = $state(300);

  const effectiveCron = $derived(customCron ? customCronValue : cron);

  function buildTypeDataJson(): string {
    switch (checkType) {
      case "HTTP": {
        const obj: Record<string, unknown> = { url: httpUrl, method: httpMethod, timeout: httpTimeout };
        if (httpExpectedCodes.trim()) obj.expectedStatusCodes = httpExpectedCodes.split(",").map(s => parseInt(s.trim())).filter(n => !isNaN(n));
        if (httpBody.trim()) obj.body = httpBody.trim();
        return JSON.stringify(obj);
      }
      case "DNS":
        return JSON.stringify({ host: dnsHost, nameServer: dnsNameServer });
      case "TCP":
        return JSON.stringify({ host: tcpHost, port: tcpPort, timeout: tcpTimeout });
      case "Ping":
        return JSON.stringify({ host: pingHost, timeout: pingTimeout });
      case "SSL":
        return JSON.stringify({ host: sslHost, port: sslPort });
      case "Heartbeat":
        return JSON.stringify({ gracePeriodSeconds: heartbeatGracePeriod });
      default:
        return "{}";
    }
  }

  function validateTypeData(): string | null {
    switch (checkType) {
      case "HTTP":
        if (!httpUrl.trim()) return "URL is required for HTTP checks.";
        try { new URL(httpUrl); } catch { return "URL must be a valid URL (e.g. https://example.com/health)."; }
        return null;
      case "DNS":
        if (!dnsHost.trim()) return "Host is required for DNS checks.";
        return null;
      case "TCP":
        if (!tcpHost.trim()) return "Host is required for TCP checks.";
        if (tcpPort < 1 || tcpPort > 65535) return "Port must be between 1 and 65535.";
        return null;
      case "Ping":
        if (!pingHost.trim()) return "Host is required for Ping checks.";
        return null;
      case "SSL":
        if (!sslHost.trim()) return "Host is required for SSL checks.";
        return null;
      case "Heartbeat":
        if (heartbeatGracePeriod < 1) return "Grace period must be at least 1 second.";
        return null;
      default:
        return null;
    }
  }

  function onNameInput() {
    if (!slugManuallyEdited) {
      slug = name.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
    }
  }

  function onSlugInput() {
    slugManuallyEdited = slug.length > 0;
  }

  async function create() {
    if (!name.trim() || !slug.trim()) { error = "Name and slug are required."; return; }
    const typeError = validateTypeData();
    if (typeError) { error = typeError; return; }
    saving = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "createCheck",
          data: {
            serviceSlug,
            slug: slug.trim(),
            name: name.trim(),
            description: description.trim() || null,
            type: checkType,
            cron: effectiveCron,
            typeDataJson: buildTypeDataJson(),
            defaultStatus,
            isActive,
          },
        }),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else goto(`/admin/services/${serviceSlug}`);
    } catch { error = "Failed to create check."; }
    finally { saving = false; }
  }
</script>


<div class="container mx-auto py-6 space-y-6">
  <!-- Breadcrumb -->
  <div class="flex items-center gap-2 text-sm text-muted-foreground">
    <a href="/admin/services" class="hover:text-foreground">Services</a>
    <span>/</span>
    <a href="/admin/services/{serviceSlug}" class="hover:text-foreground">{serviceSlug}</a>
    <span>/</span>
    <span class="text-foreground font-medium">New Check</span>
  </div>

  {#if error}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Title>Error</Alert.Title>
      <Alert.Description>{error}</Alert.Description>
    </Alert.Root>
  {/if}

  <!-- General Settings -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="flex items-center gap-2"><BlendIcon class="size-4" /> General Settings</Card.Title>
      <Card.Description>Basic information about this check</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <div class="grid grid-cols-2 gap-4">
        <Field.Field class="flex flex-col gap-1.5">
          <Field.Label for="ck-name">Name <span class="text-destructive">*</span></Field.Label>
          <Input id="ck-name" bind:value={name} oninput={onNameInput} placeholder="Health Endpoint" />
        </Field.Field>
        <Field.Field class="flex flex-col gap-1.5">
          <Field.Label for="ck-slug">Slug <span class="text-destructive">*</span></Field.Label>
          <Input id="ck-slug" bind:value={slug} oninput={onSlugInput} placeholder="health" class="font-mono" />
          <Field.Description>Unique identifier within this service</Field.Description>
        </Field.Field>
      </div>

      <Field.Field class="flex flex-col gap-1.5">
        <Field.Label for="ck-desc">Description</Field.Label>
        <Textarea id="ck-desc" bind:value={description}
          placeholder="A brief description of what this check monitors" rows={2} />
      </Field.Field>

      <div class="grid grid-cols-2 gap-4">
        <Field.Field class="flex flex-col gap-1.5">
          <Field.Label>Type <span class="text-destructive">*</span></Field.Label>
          <Select.Root type="single" value={checkType} onValueChange={(v) => v && (checkType = v)}>
            <Select.Trigger>{checkType || "Select type"}</Select.Trigger>
            <Select.Content>
              {#each CHECK_TYPES as t (t)}<Select.Item value={t}>{t}</Select.Item>{/each}
            </Select.Content>
          </Select.Root>
        </Field.Field>

        <Field.Field class="flex flex-col gap-1.5">
          <Field.Label>Cron Schedule</Field.Label>
          {#if !customCron}
            <Select.Root type="single" value={cron} onValueChange={(v) => v && (cron = v)}>
              <Select.Trigger class="font-mono">
                {CRON_PRESETS.find((p) => p.value === cron)?.label ?? cron}
              </Select.Trigger>
              <Select.Content>
                {#each CRON_PRESETS as p (p.value)}
                  <Select.Item value={p.value}>{p.label} <span class="text-muted-foreground ml-2 font-mono text-xs">{p.value}</span></Select.Item>
                {/each}
              </Select.Content>
            </Select.Root>
          {:else}
            <Input bind:value={customCronValue} placeholder="*/5 * * * *" class="font-mono" />
          {/if}
          <button type="button" class="text-xs text-muted-foreground hover:text-foreground text-left"
            onclick={() => { customCron = !customCron; if (customCron) customCronValue = cron; }}>
            {customCron ? "← Use preset" : "Enter custom cron →"}
          </button>
          <Field.Description>How often to run this check</Field.Description>
        </Field.Field>
      </div>

      <div class="grid grid-cols-2 gap-4">
        <Field.Field class="flex flex-col gap-1.5">
          <Field.Label>Default Status</Field.Label>
          <Select.Root type="single" value={defaultStatus} onValueChange={(v) => v && (defaultStatus = v)}>
            <Select.Trigger>{formatStatus(defaultStatus)}</Select.Trigger>
            <Select.Content>
              {#each DEFAULT_STATUSES as s (s)}<Select.Item value={s}>{formatStatus(s)}</Select.Item>{/each}
            </Select.Content>
          </Select.Root>
          <Field.Description>Status shown when no data is available</Field.Description>
        </Field.Field>

        <Field.Field class="flex flex-col gap-1.5">
          <Field.Label>Active</Field.Label>
          <div class="flex items-center gap-3 pt-2">
            <Switch.Root bind:checked={isActive} />
            <span class="text-sm text-muted-foreground">{isActive ? "Check is running" : "Check is paused"}</span>
          </div>
        </Field.Field>
      </div>
    </Card.Content>
  </Card.Root>

  <!-- Type-specific configuration -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Configuration</Card.Title>
      <Card.Description>Settings for the {checkType} check</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">

      {#if checkType === "HTTP"}
        <Field.Field class="flex flex-col gap-1.5">
          <Field.Label>URL <span class="text-destructive">*</span></Field.Label>
          <Input bind:value={httpUrl} placeholder="https://example.com/health" type="url" />
        </Field.Field>
        <div class="grid grid-cols-2 gap-4">
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Method</Field.Label>
            <Select.Root type="single" value={httpMethod} onValueChange={(v) => v && (httpMethod = v)}>
              <Select.Trigger class="font-mono">{httpMethod}</Select.Trigger>
              <Select.Content>
                {#each HTTP_METHODS as m (m)}<Select.Item value={m} class="font-mono">{m}</Select.Item>{/each}
              </Select.Content>
            </Select.Root>
          </Field.Field>
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Timeout (ms)</Field.Label>
            <Input bind:value={httpTimeout} type="number" min="100" max="60000" placeholder="5000" />
          </Field.Field>
        </div>
        <Field.Field class="flex flex-col gap-1.5">
          <Field.Label>Expected Status Codes</Field.Label>
          <Input bind:value={httpExpectedCodes} placeholder="200, 201, 204" />
          <Field.Description>Comma-separated list of acceptable HTTP status codes</Field.Description>
        </Field.Field>
        {#if httpMethod !== "GET" && httpMethod !== "HEAD"}
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Request Body</Field.Label>
            <Textarea bind:value={httpBody} placeholder={"{'key': 'value'}"} rows={3} class="font-mono text-sm" />
          </Field.Field>
        {/if}

      {:else if checkType === "DNS"}
        <div class="grid grid-cols-2 gap-4">
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Host <span class="text-destructive">*</span></Field.Label>
            <Input bind:value={dnsHost} placeholder="example.com" />
          </Field.Field>
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Name Server</Field.Label>
            <Input bind:value={dnsNameServer} placeholder="8.8.8.8" />
            <Field.Description>Leave blank to use system resolver</Field.Description>
          </Field.Field>
        </div>

      {:else if checkType === "TCP"}
        <div class="grid grid-cols-3 gap-4">
          <Field.Field class="col-span-2 flex flex-col gap-1.5">
            <Field.Label>Host <span class="text-destructive">*</span></Field.Label>
            <Input bind:value={tcpHost} placeholder="example.com" />
          </Field.Field>
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Port <span class="text-destructive">*</span></Field.Label>
            <Input bind:value={tcpPort} type="number" min="1" max="65535" placeholder="443" />
          </Field.Field>
        </div>
        <Field.Field class="flex flex-col gap-1.5" style="max-width: 200px">
          <Field.Label>Timeout (ms)</Field.Label>
          <Input bind:value={tcpTimeout} type="number" min="100" max="60000" placeholder="5000" />
        </Field.Field>

      {:else if checkType === "Ping"}
        <div class="grid grid-cols-2 gap-4">
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Host <span class="text-destructive">*</span></Field.Label>
            <Input bind:value={pingHost} placeholder="example.com" />
          </Field.Field>
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Timeout (ms)</Field.Label>
            <Input bind:value={pingTimeout} type="number" min="100" max="60000" placeholder="5000" />
          </Field.Field>
        </div>

      {:else if checkType === "SSL"}
        <div class="grid grid-cols-2 gap-4">
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Host <span class="text-destructive">*</span></Field.Label>
            <Input bind:value={sslHost} placeholder="example.com" />
            <Field.Description>TLS certificate will be checked for this host</Field.Description>
          </Field.Field>
          <Field.Field class="flex flex-col gap-1.5">
            <Field.Label>Port</Field.Label>
            <Input bind:value={sslPort} type="number" min="1" max="65535" placeholder="443" />
          </Field.Field>
        </div>

      {:else if checkType === "Heartbeat"}
        <Field.Field class="flex flex-col gap-1.5" style="max-width: 280px">
          <Field.Label>Grace Period (seconds)</Field.Label>
          <Input bind:value={heartbeatGracePeriod} type="number" min="1" placeholder="300" />
          <Field.Description>Time to wait after missing a heartbeat before marking as down</Field.Description>
        </Field.Field>
      {/if}

    </Card.Content>
  </Card.Root>

  <!-- Actions -->
  <div class="flex items-center justify-between">
    <Button variant="outline" href="/admin/services/{serviceSlug}">Cancel</Button>
    <Button onclick={create} disabled={saving}>
      <BlendIcon class="size-4 mr-2" />
      {saving ? "Creating…" : "Create Check"}
    </Button>
  </div>
</div>
