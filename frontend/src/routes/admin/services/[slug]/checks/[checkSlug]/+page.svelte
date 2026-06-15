<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { onMount } from "svelte";
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Switch from "$lib/components/ui/switch/index.js";
  import * as Accordion from "$lib/components/ui/accordion/index.js";
  import * as Table from "$lib/components/ui/table/index.js";
  import * as Tooltip from "$lib/components/ui/tooltip/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import BellIcon from "@lucide/svelte/icons/bell";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CheckCircleIcon from "@lucide/svelte/icons/check-circle";
  import PlayIcon from "@lucide/svelte/icons/play";
  import SaveIcon from "@lucide/svelte/icons/save";
  import Trash2Icon from "@lucide/svelte/icons/trash-2";
  import RefreshCwIcon from "@lucide/svelte/icons/refresh-cw";
  import LockIcon from "@lucide/svelte/icons/lock";
  import { format } from "date-fns";
  import type { CheckDataPointDto, AlertConfigDto, TriggerDto } from "$lib/api.js";
  import { formatStatus } from "$lib/api.js";

  const serviceSlug = $derived(page.params.slug);
  const checkSlug = $derived(page.params.checkSlug);

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

  let loading = $state(true);
  let loadError = $state("");
  let name = $state("");
  let description = $state("");
  let checkType = $state("HTTP");
  let cron = $state("* * * * *");
  let customCron = $state(false);
  let customCronValue = $state("");
  let defaultStatus = $state("NO_DATA");
  let isActive = $state(true);
  let isMultiRegion = $state(false);
  let currentStatus = $state("NO_DATA");
  let saving = $state(false);
  let running = $state(false);
  let error = $state("");
  let success = $state("");
  let activeSection = $state("general");

  let httpUrl = $state("");
  let httpMethod = $state("GET");
  let httpTimeout = $state(5000);
  let httpExpectedCodes = $state("200");
  let httpBody = $state("");
  let dnsHost = $state("");
  let dnsNameServer = $state("8.8.8.8");
  let tcpHost = $state("");
  let tcpPort = $state(443);
  let tcpTimeout = $state(5000);
  let pingHost = $state("");
  let pingTimeout = $state(5000);
  let sslHost = $state("");
  let sslPort = $state(443);
  let heartbeatGracePeriod = $state(300);

  // Alert configs state
  const ALERT_FOR_OPTIONS = ["Status", "Latency"];
  const ALERT_SEVERITY_OPTIONS = ["Info", "Warning", "Critical"];
  const STATUS_OPTIONS = ["DOWN", "DEGRADED", "UP"];
  let alertConfigs = $state<AlertConfigDto[]>([]);
  let alertConfigsLoading = $state(false);
  let alertConfigsLoaded = $state(false);
  let triggers = $state<TriggerDto[]>([]);
  let showNewAlertForm = $state(false);
  let newAlertFor = $state("Status");
  let newAlertValue = $state("DOWN");
  let newFailureThreshold = $state(1);
  let newSuccessThreshold = $state(1);
  let newAlertSeverity = $state("Warning");
  let newAlertDescription = $state("");
  let newAlertTriggerIds = $state<number[]>([]);
  let savingAlertConfig = $state(false);

  let logs = $state<CheckDataPointDto[]>([]);
  let logsLoading = $state(false);
  let logsLoaded = $state(false);
  let historyDesktop = $state<number | null>(null);
  let historyMobile = $state<number | null>(null);
  let originalHistoryDesktop = $state<number | null>(null);
  let originalHistoryMobile = $state<number | null>(null);
  let savingHistory = $state(false);

  const isHistoryDirty = $derived(
    historyDesktop !== originalHistoryDesktop ||
    historyMobile !== originalHistoryMobile
  );

  const effectiveCron = $derived(customCron ? customCronValue : cron);

  function populateTypeFields(data: Record<string, unknown>) {
    switch (checkType) {
      case "HTTP":
        httpUrl = (data.url as string) ?? "";
        httpMethod = (data.method as string) ?? "GET";
        httpTimeout = (data.timeout as number) ?? 5000;
        httpExpectedCodes = Array.isArray(data.expectedStatusCodes) ? data.expectedStatusCodes.join(", ") : "200";
        httpBody = (data.body as string) ?? "";
        break;
      case "DNS":
        dnsHost = (data.host as string) ?? "";
        dnsNameServer = (data.nameServer as string) ?? "8.8.8.8";
        break;
      case "TCP":
        tcpHost = (data.host as string) ?? "";
        tcpPort = (data.port as number) ?? 443;
        tcpTimeout = (data.timeout as number) ?? 5000;
        break;
      case "Ping":
        pingHost = (data.host as string) ?? "";
        pingTimeout = (data.timeout as number) ?? 5000;
        break;
      case "SSL":
        sslHost = (data.host as string) ?? "";
        sslPort = (data.port as number) ?? 443;
        break;
      case "Heartbeat":
        heartbeatGracePeriod = (data.gracePeriodSeconds as number) ?? 300;
        break;
    }
  }

  function buildTypeDataJson(): string {
    switch (checkType) {
      case "HTTP": {
        const obj: Record<string, unknown> = { url: httpUrl, method: httpMethod, timeout: httpTimeout };
        if (httpExpectedCodes.trim()) obj.expectedStatusCodes = httpExpectedCodes.split(",").map(s => parseInt(s.trim())).filter(n => !isNaN(n));
        if (httpBody.trim()) obj.body = httpBody.trim();
        return JSON.stringify(obj);
      }
      case "DNS": return JSON.stringify({ host: dnsHost, nameServer: dnsNameServer });
      case "TCP": return JSON.stringify({ host: tcpHost, port: tcpPort, timeout: tcpTimeout });
      case "Ping": return JSON.stringify({ host: pingHost, timeout: pingTimeout });
      case "SSL": return JSON.stringify({ host: sslHost, port: sslPort });
      case "Heartbeat": return JSON.stringify({ gracePeriodSeconds: heartbeatGracePeriod });
      default: return "{}";
    }
  }

  function validateTypeData(): string | null {
    switch (checkType) {
      case "HTTP":
        if (!httpUrl.trim()) return "URL is required for HTTP checks.";
        try { new URL(httpUrl); } catch { return "URL must be a valid URL."; }
        return null;
      case "DNS": if (!dnsHost.trim()) return "Host is required."; return null;
      case "TCP": if (!tcpHost.trim()) return "Host is required."; return null;
      case "Ping": if (!pingHost.trim()) return "Host is required."; return null;
      case "SSL": if (!sslHost.trim()) return "Host is required."; return null;
      case "Heartbeat": if (heartbeatGracePeriod < 1) return "Grace period must be at least 1 second."; return null;
      default: return null;
    }
  }

  async function loadCheck() {
    loading = true; loadError = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getCheck", data: { serviceSlug, checkSlug } }),
      });
      const result = await res.json();
      if (result.error) { loadError = result.error; return; }
      name = result.name;
      description = result.description ?? "";
      checkType = result.type;
      currentStatus = result.currentStatus;
      defaultStatus = result.defaultStatus;
      isActive = result.isActive;
      isMultiRegion = result.isMultiRegion ?? false;
      if (CRON_PRESETS.some((p) => p.value === result.cron)) {
        cron = result.cron; customCron = false;
      } else {
        customCron = true; customCronValue = result.cron;
      }
      try { populateTypeFields(JSON.parse(result.typeDataJson || "{}")); } catch { /* ignore */ }
      historyDesktop = result.historyDaysDesktop ?? null;
      historyMobile = result.historyDaysMobile ?? null;
      originalHistoryDesktop = historyDesktop;
      originalHistoryMobile = historyMobile;
    } catch { loadError = "Failed to load check."; }
    finally { loading = false; }
  }

  async function fetchLogs() {
    logsLoading = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getCheckLogs", data: { serviceSlug, checkSlug, limit: 20 } }),
      });
      const result = await res.json();
      if (!result.error) logs = result;
    } catch { /* ignore */ }
    finally { logsLoading = false; logsLoaded = true; }
  }

  async function save() {
    const typeError = validateTypeData();
    if (typeError) { error = typeError; return; }
    saving = true; error = ""; success = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "updateCheck",
          data: { serviceSlug, checkSlug, name: name.trim(), description: description.trim() || null,
            type: checkType, cron: effectiveCron, typeDataJson: buildTypeDataJson(), defaultStatus, isActive, isMultiRegion },
        }),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else { success = "Check updated successfully."; currentStatus = result.currentStatus; }
    } catch { error = "Failed to save check."; }
    finally { saving = false; }
  }

  async function runNow() {
    running = true; error = ""; success = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "runCheck", data: { serviceSlug, checkSlug } }),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else { success = "Check executed. Status will update shortly."; await fetchLogs(); }
    } catch { error = "Failed to run check."; }
    finally { running = false; }
  }

  async function saveHistoryDays() {
    savingHistory = true; error = ""; success = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "updateCheck",
          data: {
            serviceSlug, checkSlug,
            historyDaysDesktop: historyDesktop,
            historyDaysMobile: historyMobile,
          },
        }),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else {
        success = "History settings saved.";
        originalHistoryDesktop = historyDesktop;
        originalHistoryMobile = historyMobile;
      }
    } catch { error = "Failed to save history settings."; }
    finally { savingHistory = false; }
  }

  async function deleteCheck() {
    if (!confirm(`Delete check "${name}"? This cannot be undone.`)) return;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "deleteCheck", data: { serviceSlug, checkSlug } }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      goto(`/admin/services/${serviceSlug}`);
    } catch { error = "Failed to delete check."; }
  }

  function statusVariant(s: string) {
    return s === "UP" ? "default" : s === "DOWN" ? "destructive" : "secondary";
  }

  function formatTs(ts: number): string {
    try { return format(new Date(ts * 1000), "yyyy-MM-dd HH:mm:ss"); } catch { return String(ts); }
  }

  async function fetchAlertConfigs() {
    alertConfigsLoading = true;
    try {
      const [configs, allTriggers] = await Promise.all([
        fetch("/admin/api", { method: "POST", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ action: "getAlertConfigs", data: { serviceSlug, checkSlug } }) }).then(r => r.json()),
        fetch("/admin/api", { method: "POST", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ action: "getTriggers", data: {} }) }).then(r => r.json()),
      ]);
      if (!configs.error) alertConfigs = configs;
      if (!allTriggers.error) triggers = allTriggers;
    } catch { /* ignore */ }
    finally { alertConfigsLoading = false; alertConfigsLoaded = true; }
  }

  async function createAlertConfig() {
    savingAlertConfig = true;
    try {
      const res = await fetch("/admin/api", { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "createAlertConfig", data: {
          serviceSlug, checkSlug,
          alertFor: newAlertFor, alertValue: newAlertValue,
          failureThreshold: newFailureThreshold, successThreshold: newSuccessThreshold,
          severity: newAlertSeverity, description: newAlertDescription || null,
          triggerIds: newAlertTriggerIds,
        }}) });
      const result = await res.json();
      if (!result.error) {
        alertConfigs = [...alertConfigs, result];
        showNewAlertForm = false;
        newAlertFor = "Status"; newAlertValue = "DOWN"; newFailureThreshold = 1;
        newSuccessThreshold = 1; newAlertSeverity = "Warning"; newAlertDescription = "";
        newAlertTriggerIds = [];
      } else { error = result.error; }
    } catch { error = "Failed to create alert config."; }
    finally { savingAlertConfig = false; }
  }

  async function deleteAlertConfig(id: number) {
    if (!confirm("Delete this alert configuration?")) return;
    try {
      await fetch("/admin/api", { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "deleteAlertConfig", data: { serviceSlug, checkSlug, id } }) });
      alertConfigs = alertConfigs.filter(c => c.id !== id);
    } catch { error = "Failed to delete alert config."; }
  }

  onMount(() => { loadCheck(); });

  $effect(() => {
    if (activeSection === "logs" && !logsLoaded && !logsLoading) fetchLogs();
    if (activeSection === "alerts" && !alertConfigsLoaded && !alertConfigsLoading) fetchAlertConfigs();
  });
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div class="flex items-center justify-between">
    <div class="flex items-center gap-2 text-sm text-muted-foreground">
      <a href="/admin/services" class="hover:text-foreground">Services</a>
      <span>/</span>
      <a href="/admin/services/{serviceSlug}" class="hover:text-foreground">{serviceSlug}</a>
      <span>/</span>
      <span class="text-foreground font-medium">{name || checkSlug}</span>
    </div>
    <div class="flex items-center gap-2">
      {#if !loading}
        <Badge variant="secondary">{checkType}</Badge>
        <Badge variant={statusVariant(currentStatus)}>{formatStatus(currentStatus)}</Badge>
        {#if !isActive}<Badge variant="outline">Paused</Badge>{/if}
      {/if}
      <Button variant="outline" size="sm" onclick={runNow} disabled={running || loading}>
        {#if running}<Spinner class="size-3 mr-1" />{:else}<PlayIcon class="size-3 mr-1" />{/if}
        Run now
      </Button>
    </div>
  </div>

  {#if loading}
    <div class="flex items-center gap-3 py-12 justify-center text-muted-foreground">
      <Spinner class="size-5" /> Loading check…
    </div>
  {:else if loadError}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Title>Failed to load</Alert.Title>
      <Alert.Description>{loadError}</Alert.Description>
    </Alert.Root>
  {:else}
    {#if error}
      <Alert.Root variant="destructive">
        <AlertCircleIcon />
        <Alert.Title>Error</Alert.Title>
        <Alert.Description>{error}</Alert.Description>
      </Alert.Root>
    {/if}
    {#if success}
      <Alert.Root class="border-green-200 bg-green-50 text-green-800 dark:bg-green-950 dark:border-green-800 dark:text-green-300">
        <CheckCircleIcon />
        <Alert.Description>{success}</Alert.Description>
      </Alert.Root>
    {/if}

    <Accordion.Root type="single" class="w-full" bind:value={activeSection}>

      <Accordion.Item value="general">
        <Accordion.Trigger>General Settings</Accordion.Trigger>
        <Accordion.Content>
          <Card.Root>
            <Card.Content class="space-y-4 pt-4">
              <div class="grid grid-cols-2 gap-4">
                <div class="flex flex-col gap-2">
                  <Label>Name <span class="text-destructive">*</span></Label>
                  <Input bind:value={name} placeholder="Health Endpoint" />
                </div>
                <div class="flex flex-col gap-2">
                  <Label>Slug</Label>
                  <Input value={checkSlug} disabled class="font-mono opacity-60" />
                  <p class="text-xs text-muted-foreground">Cannot be changed after creation</p>
                </div>
              </div>
              <div class="flex flex-col gap-2">
                <Label>Description</Label>
                <Textarea bind:value={description} placeholder="A brief description" rows={2} />
              </div>
              <div class="grid grid-cols-2 gap-4">
                <div class="flex flex-col gap-2">
                  <Label>Type</Label>
                  <Select.Root type="single" value={checkType} onValueChange={(v) => v && (checkType = v)}>
                    <Select.Trigger>{checkType}</Select.Trigger>
                    <Select.Content>
                      {#each CHECK_TYPES as t (t)}<Select.Item value={t}>{t}</Select.Item>{/each}
                    </Select.Content>
                  </Select.Root>
                </div>
                <div class="flex flex-col gap-2">
                  <Label>Cron Schedule</Label>
                  {#if !customCron}
                    <Select.Root type="single" value={cron} onValueChange={(v) => v && (cron = v)}>
                      <Select.Trigger class="font-mono">{CRON_PRESETS.find((p) => p.value === cron)?.label ?? cron}</Select.Trigger>
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
                </div>
              </div>
              <div class="grid grid-cols-3 gap-4">
                <div class="flex flex-col gap-2">
                  <Label>Default Status</Label>
                  <Select.Root type="single" value={defaultStatus} onValueChange={(v) => v && (defaultStatus = v)}>
                    <Select.Trigger>{formatStatus(defaultStatus)}</Select.Trigger>
                    <Select.Content>
                      {#each DEFAULT_STATUSES as s (s)}<Select.Item value={s}>{formatStatus(s)}</Select.Item>{/each}
                    </Select.Content>
                  </Select.Root>
                </div>
                <div class="flex flex-col gap-2">
                  <Label>Active</Label>
                  <div class="flex items-center gap-3 pt-1">
                    <Switch.Root bind:checked={isActive} />
                    <span class="text-sm text-muted-foreground">{isActive ? "Running" : "Paused"}</span>
                  </div>
                </div>
                <div class="flex flex-col gap-2">
                  <Label>Multi-region</Label>
                  <div class="flex items-center gap-3 pt-1">
                    <Switch.Root bind:checked={isMultiRegion} />
                    <span class="text-sm text-muted-foreground">{isMultiRegion ? "Enabled" : "Disabled"}</span>
                  </div>
                </div>
              </div>
            </Card.Content>
            <Card.Footer class="flex justify-end">
              <Button onclick={save} disabled={saving}>
                {#if saving}<Spinner class="size-4 mr-1" />{:else}<SaveIcon class="size-4 mr-1" />{/if}
                Save changes
              </Button>
            </Card.Footer>
          </Card.Root>
        </Accordion.Content>
      </Accordion.Item>

      <Accordion.Item value="configuration">
        <Accordion.Trigger>Configuration</Accordion.Trigger>
        <Accordion.Content>
          <Card.Root>
            <Card.Header class="pb-2">
              <Card.Description>Type-specific settings for the {checkType} check</Card.Description>
            </Card.Header>
            <Card.Content class="space-y-4">
              {#if checkType === "HTTP"}
                <div class="flex flex-col gap-2">
                  <Label>URL <span class="text-destructive">*</span></Label>
                  <Input bind:value={httpUrl} placeholder="https://example.com/health" type="url" />
                </div>
                <div class="grid grid-cols-2 gap-4">
                  <div class="flex flex-col gap-2">
                    <Label>Method</Label>
                    <Select.Root type="single" value={httpMethod} onValueChange={(v) => v && (httpMethod = v)}>
                      <Select.Trigger class="font-mono">{httpMethod}</Select.Trigger>
                      <Select.Content>
                        {#each HTTP_METHODS as m (m)}<Select.Item value={m} class="font-mono">{m}</Select.Item>{/each}
                      </Select.Content>
                    </Select.Root>
                  </div>
                  <div class="flex flex-col gap-2">
                    <Label>Timeout (ms)</Label>
                    <Input bind:value={httpTimeout} type="number" min="100" max="60000" />
                  </div>
                </div>
                <div class="flex flex-col gap-2">
                  <Label>Expected Status Codes</Label>
                  <Input bind:value={httpExpectedCodes} placeholder="200, 201, 204" />
                  <p class="text-xs text-muted-foreground">Comma-separated list</p>
                </div>
                {#if httpMethod !== "GET" && httpMethod !== "HEAD"}
                  <div class="flex flex-col gap-2">
                    <Label>Request Body</Label>
                    <Textarea bind:value={httpBody} placeholder={"{'key': 'value'}"} rows={3} class="font-mono text-sm" />
                  </div>
                {/if}
              {:else if checkType === "DNS"}
                <div class="grid grid-cols-2 gap-4">
                  <div class="flex flex-col gap-2">
                    <Label>Host <span class="text-destructive">*</span></Label>
                    <Input bind:value={dnsHost} placeholder="example.com" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <Label>Name Server</Label>
                    <Input bind:value={dnsNameServer} placeholder="8.8.8.8" />
                  </div>
                </div>
              {:else if checkType === "TCP"}
                <div class="grid grid-cols-3 gap-4">
                  <div class="col-span-2 flex flex-col gap-2">
                    <Label>Host <span class="text-destructive">*</span></Label>
                    <Input bind:value={tcpHost} placeholder="example.com" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <Label>Port <span class="text-destructive">*</span></Label>
                    <Input bind:value={tcpPort} type="number" min="1" max="65535" />
                  </div>
                </div>
                <div class="flex flex-col gap-2" style="max-width:200px">
                  <Label>Timeout (ms)</Label>
                  <Input bind:value={tcpTimeout} type="number" min="100" max="60000" />
                </div>
              {:else if checkType === "Ping"}
                <div class="grid grid-cols-2 gap-4">
                  <div class="flex flex-col gap-2">
                    <Label>Host <span class="text-destructive">*</span></Label>
                    <Input bind:value={pingHost} placeholder="example.com" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <Label>Timeout (ms)</Label>
                    <Input bind:value={pingTimeout} type="number" min="100" max="60000" />
                  </div>
                </div>
              {:else if checkType === "SSL"}
                <div class="grid grid-cols-2 gap-4">
                  <div class="flex flex-col gap-2">
                    <Label>Host <span class="text-destructive">*</span></Label>
                    <Input bind:value={sslHost} placeholder="example.com" />
                    <p class="text-xs text-muted-foreground">TLS certificate will be checked</p>
                  </div>
                  <div class="flex flex-col gap-2">
                    <Label>Port</Label>
                    <Input bind:value={sslPort} type="number" min="1" max="65535" />
                  </div>
                </div>
              {:else if checkType === "Heartbeat"}
                <div class="flex flex-col gap-2" style="max-width:280px">
                  <Label>Grace Period (seconds)</Label>
                  <Input bind:value={heartbeatGracePeriod} type="number" min="1" />
                  <p class="text-xs text-muted-foreground">Time to wait before marking as down</p>
                </div>
              {/if}
            </Card.Content>
            <Card.Footer class="flex justify-end">
              <Button onclick={save} disabled={saving}>
                {#if saving}<Spinner class="size-4 mr-1" />{:else}<SaveIcon class="size-4 mr-1" />{/if}
                Save changes
              </Button>
            </Card.Footer>
          </Card.Root>
        </Accordion.Content>
      </Accordion.Item>

      <Accordion.Item value="logs">
        <Accordion.Trigger>Recent Logs</Accordion.Trigger>
        <Accordion.Content>
          <Card.Root>
            <Card.Header>
              <div class="flex items-center justify-between">
                <div>
                  <Card.Title>Recent Logs</Card.Title>
                  <Card.Description>Last 20 execution data points</Card.Description>
                </div>
                <Button variant="ghost" size="icon" onclick={fetchLogs} disabled={logsLoading}>
                  <RefreshCwIcon class="size-4 {logsLoading ? 'animate-spin' : ''}" />
                </Button>
              </div>
            </Card.Header>
            <Card.Content>
              {#if logsLoading && logs.length === 0}
                <div class="flex justify-center py-8"><Spinner class="size-6" /></div>
              {:else if logs.length === 0}
                <div class="text-muted-foreground py-8 text-center text-sm">No execution data yet. Run the check to see logs.</div>
              {:else}
                <div class="ktable rounded-lg border">
                  <Table.Root>
                    <Table.Header>
                      <Table.Row>
                        <Table.Head class="w-44">Timestamp</Table.Head>
                        <Table.Head class="w-24">Status</Table.Head>
                        <Table.Head class="w-24">Latency</Table.Head>
                        <Table.Head class="w-28">Type</Table.Head>
                        <Table.Head>Error</Table.Head>
                      </Table.Row>
                    </Table.Header>
                    <Table.Body>
                      {#each logs as log (log.timestamp)}
                        <Table.Row>
                          <Table.Cell>
                            <span class="text-muted-foreground text-sm font-mono">{formatTs(log.timestamp)}</span>
                          </Table.Cell>
                          <Table.Cell>
                            <Badge
                              class={log.status === "UP" ? "bg-foreground text-background hover:bg-foreground/90" : ""}
                              variant={log.status === "DOWN" ? "destructive" : log.status === "UP" ? undefined : "secondary"}>
                              {formatStatus(log.status)}
                            </Badge>
                          </Table.Cell>
                          <Table.Cell>
                            {#if log.latencyMs !== null}
                              <span class="text-sm">{log.latencyMs} ms</span>
                            {:else}
                              <span class="text-muted-foreground text-sm">—</span>
                            {/if}
                          </Table.Cell>
                          <Table.Cell>
                            {#if log.dataType}
                              <Badge variant="secondary" class="text-xs">{log.dataType}</Badge>
                            {:else}
                              <span class="text-muted-foreground text-sm">—</span>
                            {/if}
                          </Table.Cell>
                          <Table.Cell>
                            {#if log.errorMessage}
                              <Tooltip.Root>
                                <Tooltip.Trigger>
                                  <span class="text-destructive line-clamp-1 max-w-xs text-sm">{log.errorMessage}</span>
                                </Tooltip.Trigger>
                                <Tooltip.Content class="max-w-md">
                                  <p class="break-words">{log.errorMessage}</p>
                                </Tooltip.Content>
                              </Tooltip.Root>
                            {:else}
                              <span class="text-muted-foreground text-sm">—</span>
                            {/if}
                          </Table.Cell>
                        </Table.Row>
                      {/each}
                    </Table.Body>
                  </Table.Root>
                </div>
              {/if}
            </Card.Content>
          </Card.Root>
        </Accordion.Content>
      </Accordion.Item>

      <Accordion.Item value="history">
        <Accordion.Trigger>Status History</Accordion.Trigger>
        <Accordion.Content>
          <Card.Root>
            <Card.Content class="space-y-4 pt-4">
              <p class="text-sm text-muted-foreground">
                Override the service-level history display settings for this check.
                Leave blank to inherit from the service. Values must be between 1 and 365.
              </p>
              <div class="grid grid-cols-2 gap-4">
                <div class="flex flex-col gap-2">
                  <Label for="chk-hist-desktop">Desktop (days)</Label>
                  <Input id="chk-hist-desktop" type="number" min="1" max="365"
                    value={historyDesktop ?? ""}
                    oninput={(e) => { const v = parseInt((e.target as HTMLInputElement).value); historyDesktop = isNaN(v) ? null : v; }} />
                  <p class="text-xs text-muted-foreground">Leave empty to use service default</p>
                </div>
                <div class="flex flex-col gap-2">
                  <Label for="chk-hist-mobile">Mobile (days)</Label>
                  <Input id="chk-hist-mobile" type="number" min="1" max="365"
                    value={historyMobile ?? ""}
                    oninput={(e) => { const v = parseInt((e.target as HTMLInputElement).value); historyMobile = isNaN(v) ? null : v; }} />
                  <p class="text-xs text-muted-foreground">Leave empty to use service default</p>
                </div>
              </div>
            </Card.Content>
            <Card.Footer class="flex justify-end">
              <Button onclick={saveHistoryDays} disabled={savingHistory || !isHistoryDirty}>
                {#if savingHistory}<Spinner class="size-4 mr-1" />{:else}<SaveIcon class="size-4 mr-1" />{/if}
                Save history settings
              </Button>
            </Card.Footer>
          </Card.Root>
        </Accordion.Content>
      </Accordion.Item>

      <Accordion.Item value="alerts">
        <Accordion.Trigger>
          <div class="flex items-center gap-2">
            <BellIcon class="size-4" /> Alert Configurations
          </div>
        </Accordion.Trigger>
        <Accordion.Content>
          <Card.Root>
            <Card.Header>
              <div class="flex items-center justify-between">
                <div>
                  <Card.Title>Alert Configurations</Card.Title>
                  <Card.Description>Define when notifications are sent for this check.</Card.Description>
                </div>
                <Button size="sm" onclick={() => { showNewAlertForm = true; fetchAlertConfigs(); }}>
                  <PlusIcon class="size-4 mr-1" /> Add alert
                </Button>
              </div>
            </Card.Header>
            <Card.Content class="space-y-4">
              {#if alertConfigsLoading}
                <div class="flex justify-center py-6"><Spinner class="size-5" /></div>
              {:else if alertConfigs.length === 0 && !showNewAlertForm}
                <div class="text-muted-foreground py-6 text-center text-sm">
                  No alert configurations yet. Add one to get notified when this check fails.
                </div>
              {:else}
                {#each alertConfigs as cfg (cfg.id)}
                  <div class="rounded-lg border p-4 flex items-start justify-between gap-4">
                    <div class="space-y-1">
                      <div class="flex items-center gap-2">
                        <Badge variant={cfg.isAlerting ? "destructive" : "secondary"}>
                          {cfg.isAlerting ? "ALERTING" : "OK"}
                        </Badge>
                        <Badge variant="outline">{cfg.severity}</Badge>
                        <span class="text-sm font-medium">{cfg.alertFor}: {cfg.alertValue}</span>
                      </div>
                      <p class="text-xs text-muted-foreground">
                        Fire after {cfg.failureThreshold} failure{cfg.failureThreshold !== 1 ? "s" : ""} ·
                        Resolve after {cfg.successThreshold} success{cfg.successThreshold !== 1 ? "es" : ""}
                        {cfg.triggerIds.length > 0 ? ` · ${cfg.triggerIds.length} trigger${cfg.triggerIds.length !== 1 ? "s" : ""}` : " · No triggers"}
                      </p>
                      {#if cfg.description}
                        <p class="text-xs text-muted-foreground">{cfg.description}</p>
                      {/if}
                    </div>
                    <Button variant="ghost" size="icon" class="text-destructive hover:text-destructive shrink-0"
                      onclick={() => deleteAlertConfig(cfg.id)}>
                      <Trash2Icon class="size-4" />
                    </Button>
                  </div>
                {/each}
              {/if}

              {#if showNewAlertForm}
                <div class="rounded-lg border p-4 space-y-4 bg-muted/30">
                  <p class="text-sm font-medium">New alert configuration</p>
                  <div class="grid grid-cols-2 gap-4">
                    <div class="flex flex-col gap-2">
                      <Label>Alert For</Label>
                      <Select.Root type="single" value={newAlertFor} onValueChange={(v) => v && (newAlertFor = v)}>
                        <Select.Trigger>{newAlertFor}</Select.Trigger>
                        <Select.Content>
                          {#each ALERT_FOR_OPTIONS as o (o)}<Select.Item value={o}>{o}</Select.Item>{/each}
                        </Select.Content>
                      </Select.Root>
                    </div>
                    <div class="flex flex-col gap-2">
                      <Label>{newAlertFor === "Status" ? "Trigger on Status" : "Max Latency (ms)"}</Label>
                      {#if newAlertFor === "Status"}
                        <Select.Root type="single" value={newAlertValue} onValueChange={(v) => v && (newAlertValue = v)}>
                          <Select.Trigger>{newAlertValue}</Select.Trigger>
                          <Select.Content>
                            {#each STATUS_OPTIONS as s (s)}<Select.Item value={s}>{s}</Select.Item>{/each}
                          </Select.Content>
                        </Select.Root>
                      {:else}
                        <Input bind:value={newAlertValue} type="number" min="1" placeholder="e.g. 2000" />
                      {/if}
                    </div>
                  </div>
                  <div class="grid grid-cols-3 gap-4">
                    <div class="flex flex-col gap-2">
                      <Label>Failure threshold</Label>
                      <Input bind:value={newFailureThreshold} type="number" min="1" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <Label>Recovery threshold</Label>
                      <Input bind:value={newSuccessThreshold} type="number" min="1" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <Label>Severity</Label>
                      <Select.Root type="single" value={newAlertSeverity} onValueChange={(v) => v && (newAlertSeverity = v)}>
                        <Select.Trigger>{newAlertSeverity}</Select.Trigger>
                        <Select.Content>
                          {#each ALERT_SEVERITY_OPTIONS as s (s)}<Select.Item value={s}>{s}</Select.Item>{/each}
                        </Select.Content>
                      </Select.Root>
                    </div>
                  </div>
                  <div class="flex flex-col gap-2">
                    <Label>Description (optional)</Label>
                    <Input bind:value={newAlertDescription} placeholder="Brief description of this alert rule" />
                  </div>
                  {#if triggers.length > 0}
                    <div class="flex flex-col gap-2">
                      <Label>Notification channels</Label>
                      {#if newAlertTriggerIds.length === 0}
                        <p class="text-xs text-amber-500">No channel selected — the alert will still fire but no notification will be sent.</p>
                      {/if}
                      <div class="flex flex-wrap gap-2">
                        {#each triggers as t (t.id)}
                          <label class="flex items-center gap-2 {t.isLocked ? 'cursor-not-allowed opacity-70' : 'cursor-pointer'}">
                            <input type="checkbox"
                              checked={newAlertTriggerIds.includes(t.id) || t.isGlobal}
                              disabled={t.isLocked}
                              onchange={(e) => {
                                if (t.isLocked) return;
                                if ((e.target as HTMLInputElement).checked) {
                                  newAlertTriggerIds = [...newAlertTriggerIds, t.id];
                                } else {
                                  newAlertTriggerIds = newAlertTriggerIds.filter(id => id !== t.id);
                                }
                              }} />
                            <span class="text-sm flex items-center gap-1">
                              {t.name} <span class="text-muted-foreground">({t.type})</span>
                              {#if t.isLocked}<LockIcon class="size-3 text-muted-foreground" />{/if}
                            </span>
                          </label>
                        {/each}
                      </div>
                    </div>
                  {:else}
                    <p class="text-xs text-muted-foreground">
                      No notification channels configured. <a href="/admin/triggers" class="underline">Create triggers</a> first.
                    </p>
                  {/if}
                  <div class="flex gap-2 justify-end">
                    <Button variant="outline" onclick={() => showNewAlertForm = false}>Cancel</Button>
                    <Button onclick={createAlertConfig} disabled={savingAlertConfig}>
                      {#if savingAlertConfig}<Spinner class="size-4 mr-1" />{/if}
                      Save alert
                    </Button>
                  </div>
                </div>
              {/if}
            </Card.Content>
          </Card.Root>
        </Accordion.Content>
      </Accordion.Item>

      <Accordion.Item value="danger">
        <Accordion.Trigger class="text-destructive hover:text-destructive">Danger Zone</Accordion.Trigger>
        <Accordion.Content>
          <Card.Root class="border-destructive/50">
            <Card.Content class="pt-4">
              <div class="flex items-center justify-between">
                <div>
                  <p class="font-medium text-sm">Delete this check</p>
                  <p class="text-xs text-muted-foreground mt-1">Permanently deletes the check and all its execution history. This cannot be undone.</p>
                </div>
                <Button variant="destructive" size="sm" onclick={deleteCheck}>
                  <Trash2Icon class="size-4 mr-1" /> Delete check
                </Button>
              </div>
            </Card.Content>
          </Card.Root>
        </Accordion.Content>
      </Accordion.Item>

    </Accordion.Root>
  {/if}
</div>
