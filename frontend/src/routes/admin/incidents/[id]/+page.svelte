<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { slide } from "svelte/transition";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Switch } from "$lib/components/ui/switch/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Dialog from "$lib/components/ui/dialog/index.js";
  import * as Breadcrumb from "$lib/components/ui/breadcrumb/index.js";
  import SaveIcon from "lucide-svelte/icons/save";
  import PlusIcon from "lucide-svelte/icons/plus";
  import TrashIcon from "lucide-svelte/icons/trash";
  import PencilIcon from "lucide-svelte/icons/pencil";
  import CheckIcon from "lucide-svelte/icons/check";
  import XIcon from "lucide-svelte/icons/x";
  import ExternalLinkIcon from "lucide-svelte/icons/external-link";
  import type { Incident, IncidentComment, ServiceDto } from "$lib/api.js";

  const incidentId = Number(page.params.id);

  // ── State ──────────────────────────────────────────────────────────────────
  let loading = $state(true);
  let saving = $state(false);
  let error = $state<string | null>(null);
  let incident = $state<Incident | null>(null);

  // Edit form
  let title = $state("");
  let startDateTimeLocal = $state("");
  let isGlobal = $state(false);
  let incidentState = $state("Investigating");

  const STATES = ["Investigating", "Identified", "Monitoring", "Resolved"];

  const stateColor: Record<string, string> = {
    Investigating: "bg-amber-100 text-amber-700 border-amber-300 dark:bg-amber-900/30 dark:text-amber-400",
    Identified:    "bg-orange-100 text-orange-700 border-orange-300 dark:bg-orange-900/30 dark:text-orange-400",
    Monitoring:    "bg-blue-100 text-blue-700 border-blue-300 dark:bg-blue-900/30 dark:text-blue-400",
    Resolved:      "bg-green-100 text-green-700 border-green-300 dark:bg-green-900/30 dark:text-green-400",
  };

  // Comments
  let addCommentOpen = $state(false);
  let newCommentText = $state("");
  let newCommentState = $state("Investigating");
  let savingComment = $state(false);
  let editingComment = $state<IncidentComment | null>(null);
  let editCommentText = $state("");
  let editCommentState = $state("Investigating");

  // Affected services
  let availableServices = $state<ServiceDto[]>([]);
  let addServiceDialogOpen = $state(false);
  let selectedSlug = $state("");
  let selectedImpact = $state<"DOWN" | "DEGRADED">("DOWN");
  let loadingServices = $state(false);

  // ── Helpers ────────────────────────────────────────────────────────────────
  function toLocalDatetimeString(ts: number): string {
    const d = new Date(ts * 1000);
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  function fromLocalDatetime(s: string): number {
    return s ? Math.floor(new Date(s).getTime() / 1000) : Math.floor(Date.now() / 1000);
  }

  function fmtTs(ts: number): string {
    return new Date(ts * 1000).toLocaleString(undefined, {
      month: "short", day: "numeric", year: "numeric", hour: "numeric", minute: "2-digit",
    });
  }

  async function api(action: string, data: Record<string, unknown>) {
    const res = await fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action, data }),
    });
    return res.json();
  }

  // ── Load ───────────────────────────────────────────────────────────────────
  async function load() {
    loading = true; error = null;
    try {
      const result = await api("getIncident", { id: incidentId });
      if (result.error) { error = result.error; return; }
      incident = result as Incident;
      title = incident.title;
      startDateTimeLocal = toLocalDatetimeString(incident.startDateTime);
      isGlobal = incident.isGlobal;
      incidentState = incident.state;
    } catch { error = "Failed to load incident."; }
    finally { loading = false; }
  }

  // ── Save details ───────────────────────────────────────────────────────────
  async function saveDetails() {
    if (!title.trim()) return;
    saving = true; error = null;
    try {
      const result = await api("updateIncident", {
        id: incidentId,
        title: title.trim(),
        startDateTime: fromLocalDatetime(startDateTimeLocal),
        state: incidentState,
        isGlobal,
      });
      if (result.error) { error = result.error; return; }
      incident = result as Incident;
    } catch { error = "Failed to save."; }
    finally { saving = false; }
  }

  // ── Delete incident ────────────────────────────────────────────────────────
  async function deleteIncident() {
    if (!confirm("Delete this incident? This cannot be undone.")) return;
    await api("deleteIncident", { id: incidentId });
    goto("/admin/incidents");
  }

  // ── Comments ───────────────────────────────────────────────────────────────
  async function addComment() {
    if (!newCommentText.trim()) return;
    savingComment = true;
    try {
      await api("addComment", { id: incidentId, comment: newCommentText.trim(), state: newCommentState });
      newCommentText = ""; newCommentState = "Investigating"; addCommentOpen = false;
      await load();
    } finally { savingComment = false; }
  }

  function startEditComment(c: IncidentComment) {
    editingComment = c;
    editCommentText = c.comment;
    editCommentState = c.state;
  }

  async function saveEditComment() {
    if (!editingComment || !editCommentText.trim()) return;
    await api("updateComment", { id: incidentId, commentId: editingComment.id, comment: editCommentText.trim(), state: editCommentState });
    editingComment = null;
    await load();
  }

  async function deleteComment(commentId: number) {
    if (!confirm("Delete this update?")) return;
    await api("deleteComment", { id: incidentId, commentId });
    await load();
  }

  // ── Services ───────────────────────────────────────────────────────────────
  async function openAddService() {
    addServiceDialogOpen = true;
    if (availableServices.length > 0) return;
    loadingServices = true;
    try {
      const result = await api("getServices", {});
      availableServices = result.services ?? result ?? [];
      selectedSlug = availableServices[0]?.slug ?? "";
    } finally { loadingServices = false; }
  }

  async function addService() {
    if (!selectedSlug) return;
    const result = await api("addIncidentService", { id: incidentId, serviceSlug: selectedSlug, impact: selectedImpact });
    incident = result as Incident;
    addServiceDialogOpen = false;
  }

  async function removeService(slug: string) {
    const result = await api("removeIncidentService", { id: incidentId, serviceSlug: slug });
    incident = result as Incident;
  }

  // ── Init ───────────────────────────────────────────────────────────────────
  load();
</script>


{#if loading}
  <div class="container mx-auto py-12 text-center text-muted-foreground">Loading…</div>
{:else if error && !incident}
  <div class="container mx-auto py-12 text-center text-destructive">{error}</div>
{:else if incident}
  <div class="container mx-auto py-8 flex flex-col gap-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <Breadcrumb.Root>
        <Breadcrumb.List>
          <Breadcrumb.Item><Breadcrumb.Link href="/admin/incidents">Incidents</Breadcrumb.Link></Breadcrumb.Item>
          <Breadcrumb.Separator />
          <Breadcrumb.Item><Breadcrumb.Page>Edit Incident #{incidentId}</Breadcrumb.Page></Breadcrumb.Item>
        </Breadcrumb.List>
      </Breadcrumb.Root>
      <div class="flex items-center gap-2">
        <Button variant="outline" href="/" target="_blank">
          <ExternalLinkIcon class="size-4 mr-1" /> View
        </Button>
        <Button variant="destructive" onclick={deleteIncident}>
          <TrashIcon class="size-4 mr-1" /> Delete
        </Button>
      </div>
    </div>

    {#if error}
      <p class="text-sm text-destructive">{error}</p>
    {/if}

    <!-- Details card -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Incident Details</Card.Title>
        <Card.Description>Edit incident details and manage updates</Card.Description>
      </Card.Header>
      <Card.Content class="flex flex-col gap-5">
        <!-- State badge -->
        <div>
          <span class="inline-flex items-center rounded-full border px-3 py-1 text-xs font-semibold uppercase tracking-wide {stateColor[incidentState] ?? ''}">
            {incidentState}
          </span>
        </div>

        <!-- Title -->
        <div class="flex flex-col gap-1.5">
          <Label for="title">Title <span class="text-destructive">*</span></Label>
          <Input id="title" bind:value={title} />
        </div>

        <!-- Start Date/Time -->
        <div class="flex flex-col gap-1.5">
          <Label for="start">Start Date/Time <span class="text-destructive">*</span></Label>
          <Input id="start" type="datetime-local" bind:value={startDateTimeLocal} class="w-64" />
          <p class="text-xs text-muted-foreground">Enter time in your local timezone. It will be stored as UTC.</p>
        </div>

        <!-- State selector -->
        <div class="flex flex-col gap-1.5">
          <Label>State</Label>
          <Select.Root type="single" value={incidentState} onValueChange={(v) => v && (incidentState = v)}>
            <Select.Trigger class="w-48">{incidentState}</Select.Trigger>
            <Select.Content>
              {#each STATES as s (s)}<Select.Item value={s}>{s}</Select.Item>{/each}
            </Select.Content>
          </Select.Root>
        </div>

        <!-- Global toggle -->
        <div class="flex items-center justify-between rounded-lg border px-4 py-3">
          <div class="flex flex-col gap-0.5">
            <span class="text-sm font-medium">Global Incident</span>
            <span class="text-xs text-muted-foreground">When enabled, this incident will be visible on all status pages</span>
          </div>
          <Switch bind:checked={isGlobal} />
        </div>

        <!-- Affected services -->
        <div class="flex flex-col gap-3">
          <div class="flex items-center justify-between">
            <Label>Affected Services <span class="text-muted-foreground font-normal">(Optional)</span></Label>
            <Button variant="outline" size="sm" onclick={openAddService}>
              <PlusIcon class="size-4 mr-1" /> Add Monitor
            </Button>
          </div>
          {#if !incident.services || incident.services.length === 0}
            <p class="text-sm text-muted-foreground">No monitors selected</p>
          {:else}
            <div class="flex flex-col gap-2">
              {#each incident.services as svc (svc.serviceSlug)}
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
      </Card.Content>
      <Card.Footer class="justify-end border-t pt-4">
        <Button onclick={saveDetails} disabled={saving || !title.trim()}>
          {#if saving}
            <span class="mr-2 inline-block size-4 animate-spin rounded-full border-2 border-current border-t-transparent"></span>
            Saving…
          {:else}
            <SaveIcon class="size-4 mr-2" /> Save Changes
          {/if}
        </Button>
      </Card.Footer>
    </Card.Root>

    <!-- Updates card -->
    <Card.Root>
      <Card.Header>
        <div class="flex items-center justify-between">
          <div>
            <Card.Title>Updates</Card.Title>
            <Card.Description>Timeline of status updates for this incident</Card.Description>
          </div>
          <Button onclick={() => (addCommentOpen = !addCommentOpen)}>
            <PlusIcon class="size-4 mr-1" /> Add Update
          </Button>
        </div>
      </Card.Header>
      <Card.Content class="flex flex-col gap-4">
        <!-- Add comment form -->
        {#if addCommentOpen}
          <div transition:slide={{ duration: 180 }} class="flex flex-col gap-3 rounded-lg border p-4 bg-muted/30">
            <div class="flex flex-col gap-1.5">
              <Label>State</Label>
              <Select.Root type="single" value={newCommentState} onValueChange={(v) => v && (newCommentState = v)}>
                <Select.Trigger class="w-48">{newCommentState}</Select.Trigger>
                <Select.Content>
                  {#each STATES as s (s)}<Select.Item value={s}>{s}</Select.Item>{/each}
                </Select.Content>
              </Select.Root>
            </div>
            <Textarea bind:value={newCommentText} placeholder="Describe the update…" rows={3} class="font-mono text-sm" />
            <div class="flex gap-2">
              <Button size="sm" onclick={addComment} disabled={savingComment || !newCommentText.trim()}>
                {savingComment ? "Saving…" : "Add Update"}
              </Button>
              <Button size="sm" variant="outline" onclick={() => { addCommentOpen = false; newCommentText = ""; }}>Cancel</Button>
            </div>
          </div>
        {/if}

        <!-- Comment list -->
        {#if !incident.comments || incident.comments.length === 0}
          <p class="text-sm text-muted-foreground text-center py-4">No updates yet</p>
        {:else}
          {#each [...incident.comments].reverse() as comment (comment.id)}
            <div class="flex flex-col gap-2 rounded-lg border p-4">
              {#if editingComment?.id === comment.id}
                <!-- Edit mode -->
                <div transition:slide={{ duration: 150 }} class="flex flex-col gap-3">
                  <Select.Root type="single" value={editCommentState} onValueChange={(v) => v && (editCommentState = v)}>
                    <Select.Trigger class="w-48">{editCommentState}</Select.Trigger>
                    <Select.Content>
                      {#each STATES as s (s)}<Select.Item value={s}>{s}</Select.Item>{/each}
                    </Select.Content>
                  </Select.Root>
                  <Textarea bind:value={editCommentText} rows={3} class="font-mono text-sm" />
                  <div class="flex gap-2">
                    <Button size="sm" onclick={saveEditComment}>
                      <CheckIcon class="size-3.5 mr-1" /> Save
                    </Button>
                    <Button size="sm" variant="outline" onclick={() => (editingComment = null)}>
                      <XIcon class="size-3.5 mr-1" /> Cancel
                    </Button>
                  </div>
                </div>
              {:else}
                <div class="flex items-start justify-between gap-2">
                  <div class="flex flex-col gap-1">
                    <div class="flex items-center gap-2">
                      <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold uppercase {stateColor[comment.state] ?? ''}">
                        {comment.state}
                      </span>
                      <span class="text-xs text-muted-foreground">{fmtTs(comment.commentedAt)}</span>
                    </div>
                    <p class="text-sm mt-1">{comment.comment}</p>
                  </div>
                  <div class="flex items-center gap-1 shrink-0">
                    <Button variant="ghost" size="icon" class="size-7" onclick={() => startEditComment(comment)}>
                      <PencilIcon class="size-3.5" />
                    </Button>
                    <Button variant="ghost" size="icon" class="size-7 text-muted-foreground hover:text-destructive" onclick={() => deleteComment(comment.id)}>
                      <TrashIcon class="size-3.5" />
                    </Button>
                  </div>
                </div>
              {/if}
            </div>
          {/each}
        {/if}
      </Card.Content>
    </Card.Root>
  </div>
{/if}

<!-- Add service dialog -->
<Dialog.Root bind:open={addServiceDialogOpen}>
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
                {#each availableServices.filter(s => !incident?.services?.some(a => a.serviceSlug === s.slug)) as svc (svc.slug)}
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
        <Button variant="outline" onclick={() => (addServiceDialogOpen = false)}>Cancel</Button>
        <Button onclick={addService} disabled={!selectedSlug}>Add</Button>
      </Dialog.Footer>
    </Dialog.Content>
  </Dialog.Portal>
</Dialog.Root>
