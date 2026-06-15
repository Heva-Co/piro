<script lang="ts">
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Switch } from "$lib/components/ui/switch/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Dialog from "$lib/components/ui/dialog/index.js";
  import * as Breadcrumb from "$lib/components/ui/breadcrumb/index.js";
  import PlusIcon from "lucide-svelte/icons/plus";
  import TrashIcon from "lucide-svelte/icons/trash";
  import SaveIcon from "lucide-svelte/icons/save";
  import type { ServiceDto } from "$lib/api.js";
  import { goto } from "$app/navigation";

  // Form state
  let title = $state("");
  let isGlobal = $state(true);
  let firstComment = $state("");
  let saving = $state(false);
  let error = $state<string | null>(null);

  // Start datetime — initialized to now in local time
  function toLocalDatetimeString(ts: number): string {
    const d = new Date(ts * 1000);
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  let startDateTimeLocal = $state(toLocalDatetimeString(Math.floor(Date.now() / 1000)));

  function startDateTimeTs(): number {
    return startDateTimeLocal ? Math.floor(new Date(startDateTimeLocal).getTime() / 1000) : Math.floor(Date.now() / 1000);
  }

  // Affected services
  type AffectedService = { serviceSlug: string; impact: "DOWN" | "DEGRADED" };
  let affectedServices = $state<AffectedService[]>([]);

  // Add-service dialog
  let addDialogOpen = $state(false);
  let availableServices = $state<ServiceDto[]>([]);
  let selectedSlug = $state("");
  let selectedImpact = $state<"DOWN" | "DEGRADED">("DOWN");
  let loadingServices = $state(false);

  async function openAddDialog() {
    addDialogOpen = true;
    if (availableServices.length > 0) return;
    loadingServices = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getServices", data: {} }),
      });
      const result = await res.json();
      availableServices = result.services ?? result ?? [];
      selectedSlug = availableServices[0]?.slug ?? "";
    } catch { /* ignore */ }
    finally { loadingServices = false; }
  }

  function addService() {
    if (!selectedSlug) return;
    if (affectedServices.some((s) => s.serviceSlug === selectedSlug)) { addDialogOpen = false; return; }
    affectedServices = [...affectedServices, { serviceSlug: selectedSlug, impact: selectedImpact }];
    addDialogOpen = false;
    selectedSlug = availableServices[0]?.slug ?? "";
    selectedImpact = "DOWN";
  }

  function removeService(slug: string) {
    affectedServices = affectedServices.filter((s) => s.serviceSlug !== slug);
  }

  // Create
  async function create() {
    if (!title.trim()) { error = "Title is required."; return; }
    saving = true; error = null;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "createIncident",
          data: {
            title: title.trim(),
            startDateTime: startDateTimeTs(),
            state: "Investigating",
            isGlobal,
            affectedServices: isGlobal ? [] : affectedServices,
          },
        }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }

      const incidentId = result.id ?? result.incidentId;

      // Post initial comment if provided
      if (firstComment.trim() && incidentId) {
        await fetch("/admin/api", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            action: "addComment",
            data: { id: incidentId, comment: firstComment.trim(), state: "Investigating" },
          }),
        });
      }

      goto(`/admin/incidents/${incidentId}`);
    } catch { error = "Failed to create incident."; }
    finally { saving = false; }
  }
</script>


<div class="container mx-auto py-8 flex flex-col gap-6">
  <!-- Breadcrumb -->
  <Breadcrumb.Root>
    <Breadcrumb.List>
      <Breadcrumb.Item>
        <Breadcrumb.Link href="/admin/incidents">Incidents</Breadcrumb.Link>
      </Breadcrumb.Item>
      <Breadcrumb.Separator />
      <Breadcrumb.Item>
        <Breadcrumb.Page>New Incident</Breadcrumb.Page>
      </Breadcrumb.Item>
    </Breadcrumb.List>
  </Breadcrumb.Root>

  <Card.Root>
    <Card.Header>
      <Card.Title>Create New Incident</Card.Title>
      <Card.Description>Create a new incident to track</Card.Description>
    </Card.Header>

    <Card.Content class="flex flex-col gap-6">
      {#if error}
        <p class="text-sm text-destructive">{error}</p>
      {/if}

      <!-- Title -->
      <div class="flex flex-col gap-1.5">
        <Label for="title">Title <span class="text-destructive">*</span></Label>
        <Input id="title" bind:value={title} placeholder="Brief description of the incident" />
      </div>

      <!-- Start Date/Time -->
      <div class="flex flex-col gap-1.5">
        <Label for="start">Start Date/Time <span class="text-destructive">*</span></Label>
        <Input id="start" type="datetime-local" bind:value={startDateTimeLocal} class="w-64" />
        <p class="text-xs text-muted-foreground">Enter time in your local timezone. It will be stored as UTC.</p>
      </div>

      <!-- Global toggle -->
      <div class="flex items-center justify-between rounded-lg border px-4 py-3">
        <div class="flex flex-col gap-0.5">
          <span class="text-sm font-medium">Global Incident</span>
          <span class="text-xs text-muted-foreground">When enabled, this incident will be visible on all status pages</span>
        </div>
        <Switch bind:checked={isGlobal} />
      </div>

      <!-- Initial Update -->
      <div class="flex flex-col gap-1.5">
        <Label for="comment">Initial Update <span class="text-muted-foreground font-normal">(Optional)</span></Label>
        <Textarea
          id="comment"
          bind:value={firstComment}
          placeholder="Describe what's happening..."
          class="min-h-32 font-mono text-sm"
          rows={6}
        />
        <p class="text-xs text-muted-foreground">Supports Markdown. This will be added as the first update for this incident.</p>
      </div>

      <!-- Affected Services -->
      {#if !isGlobal}
        <div class="flex flex-col gap-3">
          <div class="flex items-center justify-between">
            <Label>Affected Services <span class="text-muted-foreground font-normal">(Optional)</span></Label>
            <Button variant="outline" size="sm" onclick={openAddDialog}>
              <PlusIcon class="size-4 mr-1" /> Add Service
            </Button>
          </div>

          {#if affectedServices.length === 0}
            <p class="text-sm text-muted-foreground">No services selected</p>
          {:else}
            <div class="flex flex-col gap-2">
              {#each affectedServices as svc (svc.serviceSlug)}
                <div class="flex items-center justify-between rounded-lg border px-4 py-2.5">
                  <div class="flex items-center gap-3">
                    <span class="text-sm font-medium">{svc.serviceSlug}</span>
                    <span class="text-xs px-2 py-0.5 rounded-full font-medium {svc.impact === 'DOWN' ? 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400' : 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400'}">
                      {svc.impact}
                    </span>
                  </div>
                  <Button variant="ghost" size="icon" class="size-7 text-muted-foreground hover:text-destructive" onclick={() => removeService(svc.serviceSlug)}>
                    <TrashIcon class="size-4" />
                  </Button>
                </div>
              {/each}
            </div>
          {/if}
        </div>
      {/if}
    </Card.Content>

    <Card.Footer class="justify-end border-t pt-4">
      <Button onclick={create} disabled={saving || !title.trim()}>
        {#if saving}
          <span class="mr-2 inline-block size-4 animate-spin rounded-full border-2 border-current border-t-transparent"></span>
          Creating…
        {:else}
          <SaveIcon class="size-4 mr-2" />
          Create Incident
        {/if}
      </Button>
    </Card.Footer>
  </Card.Root>
</div>

<!-- Add Service Dialog -->
<Dialog.Root bind:open={addDialogOpen}>
  <Dialog.Portal>
    <Dialog.Overlay />
    <Dialog.Content class="max-w-sm">
      <Dialog.Header>
        <Dialog.Title>Add Affected Service</Dialog.Title>
      </Dialog.Header>

      <div class="flex flex-col gap-4 py-2">
        {#if loadingServices}
          <p class="text-sm text-muted-foreground">Loading services…</p>
        {:else}
          <div class="flex flex-col gap-1.5">
            <Label>Service</Label>
            <Select.Root type="single" value={selectedSlug} onValueChange={(v) => v && (selectedSlug = v)}>
              <Select.Trigger class="w-full">{selectedSlug || "Select service…"}</Select.Trigger>
              <Select.Content>
                {#each availableServices.filter(s => !affectedServices.some(a => a.serviceSlug === s.slug)) as svc (svc.slug)}
                  <Select.Item value={svc.slug}>{svc.name}</Select.Item>
                {/each}
              </Select.Content>
            </Select.Root>
          </div>

          <div class="flex flex-col gap-1.5">
            <Label>Impact</Label>
            <Select.Root type="single" value={selectedImpact} onValueChange={(v) => v && (selectedImpact = v as "DOWN" | "DEGRADED")}>
              <Select.Trigger class="w-full">{selectedImpact}</Select.Trigger>
              <Select.Content>
                <Select.Item value="DOWN">Down</Select.Item>
                <Select.Item value="DEGRADED">Degraded</Select.Item>
              </Select.Content>
            </Select.Root>
          </div>
        {/if}
      </div>

      <Dialog.Footer>
        <Button variant="outline" onclick={() => (addDialogOpen = false)}>Cancel</Button>
        <Button onclick={addService} disabled={!selectedSlug}>Add</Button>
      </Dialog.Footer>
    </Dialog.Content>
  </Dialog.Portal>
</Dialog.Root>
