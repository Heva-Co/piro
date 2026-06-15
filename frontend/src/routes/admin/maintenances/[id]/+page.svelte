<script lang="ts">
  import type { PageData } from "./$types";
  import type { Maintenance } from "$lib/api";
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Switch } from "$lib/components/ui/switch/index.js";
  import * as Breadcrumb from "$lib/components/ui/breadcrumb/index.js";
  import * as Dialog from "$lib/components/ui/dialog/index.js";
  import CalendarIcon from "lucide-svelte/icons/calendar";
  import RefreshCwIcon from "lucide-svelte/icons/refresh-cw";
  import InfoIcon from "lucide-svelte/icons/info";
  import TrashIcon from "lucide-svelte/icons/trash-2";
  import BanIcon from "lucide-svelte/icons/ban";
  import SaveIcon from "lucide-svelte/icons/save";
  import { format } from "date-fns";

  let { data }: { data: PageData } = $props();

  const m: Maintenance = data.maintenance;

  // ── Form state (initialised from existing maintenance) ───────────────────────
  let title = $state(m.title);
  let description = $state(m.description ?? "");
  let isGlobal = $state(m.isGlobal);

  // Start datetime: convert unix → datetime-local string
  function toLocal(ts: number) {
    const d = new Date(ts * 1000);
    d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
    return d.toISOString().slice(0, 16);
  }
  let startDateTime = $state(toLocal(m.startDateTime));

  // Duration
  let durationHours = $state(Math.floor(m.durationSeconds / 3600));
  let durationMinutes = $state(Math.floor((m.durationSeconds % 3600) / 60));
  const durationSeconds = $derived(durationHours * 3600 + durationMinutes * 60);

  // Schedule type derived from RRULE
  const isRecurring = !m.rRule.includes("COUNT=1");
  let scheduleType = $state<"one-time" | "recurring">(isRecurring ? "recurring" : "one-time");

  // Recurring fields — parse from existing RRULE
  function parseFreq(r: string): "DAILY" | "WEEKLY" | "MONTHLY" {
    const m = r.match(/FREQ=(\w+)/);
    if (m?.[1] === "DAILY") return "DAILY";
    if (m?.[1] === "MONTHLY") return "MONTHLY";
    return "WEEKLY";
  }
  function parseInterval(r: string): number {
    const m = r.match(/INTERVAL=(\d+)/);
    return m ? parseInt(m[1]) : 1;
  }
  function parseDays(r: string): string[] {
    const m = r.match(/BYDAY=([^;]+)/);
    return m ? m[1].split(",") : [];
  }

  let recurringFreq = $state<"DAILY" | "WEEKLY" | "MONTHLY">(parseFreq(m.rRule));
  let recurringInterval = $state(parseInterval(m.rRule));
  let recurringDays = $state<string[]>(parseDays(m.rRule));

  const DAYS = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"] as const;
  const DAY_LABELS: Record<string, string> = { MO: "Mon", TU: "Tue", WE: "Wed", TH: "Thu", FR: "Fri", SA: "Sat", SU: "Sun" };

  function toggleDay(day: string) {
    if (recurringDays.includes(day)) recurringDays = recurringDays.filter(d => d !== day);
    else recurringDays = [...recurringDays, day];
  }

  const rrule = $derived.by(() => {
    if (scheduleType === "one-time") return "FREQ=MINUTELY;COUNT=1";
    let r = `FREQ=${recurringFreq};INTERVAL=${recurringInterval}`;
    if (recurringFreq === "WEEKLY" && recurringDays.length > 0)
      r += `;BYDAY=${recurringDays.join(",")}`;
    return r;
  });

  // ── Affected services ────────────────────────────────────────────────────────
  let selectedSlugs = $state<Set<string>>(new Set(m.serviceSlugs ?? []));
  function toggleService(slug: string) {
    const next = new Set(selectedSlugs);
    if (next.has(slug)) next.delete(slug); else next.add(slug);
    selectedSlugs = next;
  }

  // ── Actions ──────────────────────────────────────────────────────────────────
  let saving = $state(false);
  let error = $state("");
  let confirmDelete = $state(false);
  let confirmCancel = $state(false);

  async function apiPost(action: string, data: object) {
    const res = await fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action, data }),
    });
    return res.json();
  }

  async function save() {
    if (!title.trim()) { error = "Title is required."; return; }
    saving = true; error = "";
    try {
      const result = await apiPost("updateMaintenance", {
        id: m.id,
        title: title.trim(),
        description: description.trim() || undefined,
        startDateTime: Math.floor(new Date(startDateTime).getTime() / 1000),
        rRule: rrule,
        durationSeconds,
        isGlobal,
      });
      if (result.error) error = result.error;
      else goto("/admin/maintenances");
    } catch { error = "Failed to save."; }
    finally { saving = false; }
  }

  async function cancelMaintenance() {
    const result = await apiPost("cancelMaintenance", { id: m.id });
    if (result.error) error = result.error;
    else goto("/admin/maintenances");
  }

  async function deleteMaintenance() {
    const result = await apiPost("deleteMaintenance", { id: m.id });
    if (result.error) error = result.error;
    else goto("/admin/maintenances");
  }

  function fmtTs(ts: number) {
    return new Date(ts * 1000).toLocaleString(undefined, {
      month: "short", day: "numeric", year: "numeric",
      hour: "numeric", minute: "2-digit",
    });
  }
</script>


<div class="container mx-auto py-8 flex flex-col gap-6">
  <!-- Breadcrumb + actions -->
  <div class="flex items-center justify-between gap-4">
    <Breadcrumb.Root>
      <Breadcrumb.List>
        <Breadcrumb.Item><Breadcrumb.Link href="/admin/maintenances">Maintenances</Breadcrumb.Link></Breadcrumb.Item>
        <Breadcrumb.Separator />
        <Breadcrumb.Item><Breadcrumb.Page>{m.title}</Breadcrumb.Page></Breadcrumb.Item>
      </Breadcrumb.List>
    </Breadcrumb.Root>
    <div class="flex gap-2 shrink-0">
      {#if m.status === "Active"}
        <Button variant="outline" onclick={() => (confirmCancel = true)} class="gap-1.5 text-amber-600 border-amber-300 hover:bg-amber-50">
          <BanIcon class="size-4" /> Cancel
        </Button>
      {/if}
      <Button variant="destructive" onclick={() => (confirmDelete = true)} class="gap-1.5">
        <TrashIcon class="size-4" /> Delete
      </Button>
    </div>
  </div>

  <!-- Form card -->
  <div class="rounded-2xl border bg-card p-6 flex flex-col gap-6">
    <div>
      <h1 class="text-xl font-semibold">Edit Maintenance</h1>
      <p class="text-sm text-muted-foreground mt-0.5">Update this maintenance window</p>
    </div>

    {#if error}<p class="text-destructive text-sm">{error}</p>{/if}

    <!-- Schedule Type -->
    <div class="flex flex-col gap-2">
      <label class="text-sm font-medium">Schedule Type</label>
      <div class="flex gap-4">
        <label class="flex items-center gap-2 cursor-pointer text-sm">
          <input type="radio" name="scheduleType" value="one-time"
            checked={scheduleType === "one-time"}
            onchange={() => scheduleType = "one-time"} class="accent-primary" />
          <CalendarIcon class="size-4" /> One-Time
        </label>
        <label class="flex items-center gap-2 cursor-pointer text-sm">
          <input type="radio" name="scheduleType" value="recurring"
            checked={scheduleType === "recurring"}
            onchange={() => scheduleType = "recurring"} class="accent-primary" />
          <RefreshCwIcon class="size-4" /> Recurring
        </label>
      </div>
    </div>

    <!-- Title -->
    <div class="flex flex-col gap-1.5">
      <label class="text-sm font-medium">Title <span class="text-destructive">*</span></label>
      <Input bind:value={title} placeholder="Scheduled maintenance window" />
    </div>

    <!-- Description -->
    <div class="flex flex-col gap-1.5">
      <label class="text-sm font-medium">Description</label>
      <Textarea bind:value={description} placeholder="Details about the maintenance..." rows={3} />
    </div>

    <!-- Global toggle -->
    <div class="rounded-xl border px-4 py-3 flex items-center justify-between gap-4">
      <div>
        <p class="text-sm font-medium">Global Maintenance</p>
        <p class="text-xs text-muted-foreground">When enabled, this maintenance will be visible on all status pages</p>
      </div>
      <Switch bind:checked={isGlobal} />
    </div>

    <!-- Start DateTime -->
    <div class="flex flex-col gap-1.5">
      <label class="text-sm font-medium">Start Date/Time <span class="text-destructive">*</span></label>
      <Input type="datetime-local" bind:value={startDateTime} />
    </div>

    <!-- Duration -->
    <div class="flex flex-col gap-1.5">
      <label class="text-sm font-medium">Duration <span class="text-destructive">*</span></label>
      <div class="flex items-center gap-3">
        <Input type="number" min={0} bind:value={durationHours} class="w-24" />
        <span class="text-sm text-muted-foreground">hours</span>
        <Input type="number" min={0} max={59} bind:value={durationMinutes} class="w-24" />
        <span class="text-sm text-muted-foreground">minutes</span>
      </div>
      <p class="text-xs text-muted-foreground">
        Total: {durationSeconds} seconds ({Math.round(durationSeconds / 60)} minutes)
      </p>
    </div>

    <!-- Schedule Pattern -->
    <div class="rounded-xl border p-4 flex flex-col gap-4">
      <div class="flex items-center gap-2">
        <InfoIcon class="size-4 text-muted-foreground" />
        <span class="font-medium text-sm">Schedule Pattern</span>
      </div>

      {#if scheduleType === "recurring"}
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-muted-foreground uppercase tracking-wide">Frequency</label>
            <select bind:value={recurringFreq}
              class="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
              <option value="DAILY">Daily</option>
              <option value="WEEKLY">Weekly</option>
              <option value="MONTHLY">Monthly</option>
            </select>
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-muted-foreground uppercase tracking-wide">Every N occurrences</label>
            <Input type="number" min={1} bind:value={recurringInterval} />
          </div>
        </div>
        {#if recurringFreq === "WEEKLY"}
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-muted-foreground uppercase tracking-wide">Days of week</label>
            <div class="flex gap-2 flex-wrap">
              {#each DAYS as day (day)}
                <button type="button" onclick={() => toggleDay(day)}
                  class="text-xs px-3 py-1.5 rounded-full border transition-colors {recurringDays.includes(day) ? 'bg-primary text-primary-foreground border-primary' : 'hover:bg-muted'}">
                  {DAY_LABELS[day]}
                </button>
              {/each}
            </div>
          </div>
        {/if}
      {/if}

      <div class="flex flex-col gap-1">
        <label class="text-xs text-muted-foreground">iCalendar RRULE (auto-generated)</label>
        <code class="text-xs bg-muted rounded px-3 py-2 font-mono">{rrule}</code>
        {#if scheduleType === "one-time"}
          <p class="text-xs text-muted-foreground">One-time maintenance uses a fixed RRULE that triggers only once.</p>
        {/if}
      </div>
    </div>

    <!-- Affected Services -->
    {#if !isGlobal}
      <div class="flex flex-col gap-3">
        <h2 class="text-sm font-medium">Affected Services</h2>
        {#if data.services.length === 0}
          <p class="text-sm text-muted-foreground">No services available.</p>
        {:else}
          <div class="rounded-xl border p-4 flex flex-col gap-2">
            <p class="text-xs text-muted-foreground mb-1">Select services to add:</p>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-y-2 gap-x-6">
              {#each data.services as svc (svc.slug)}
                <label class="flex items-center gap-2 cursor-pointer text-sm">
                  <input type="checkbox" checked={selectedSlugs.has(svc.slug)}
                    onchange={() => toggleService(svc.slug)} class="rounded accent-primary" />
                  {svc.name}
                </label>
              {/each}
            </div>
          </div>
        {/if}
      </div>
    {/if}

    <!-- Upcoming Events -->
    {#if m.upcomingEvents?.length > 0}
      <div class="flex flex-col gap-3">
        <h2 class="text-sm font-medium">Upcoming Events</h2>
        <div class="rounded-xl border divide-y">
          {#each m.upcomingEvents as ev (ev.id)}
            <div class="px-4 py-3 flex items-center justify-between gap-4 text-sm">
              <span>{fmtTs(ev.startDateTime)}</span>
              <span class="text-muted-foreground">→</span>
              <span>{fmtTs(ev.endDateTime)}</span>
              <span class="text-xs rounded-full px-2.5 py-0.5 {ev.status === 'Ongoing' ? 'bg-blue-100 text-blue-700' : ev.status === 'Completed' ? 'bg-green-100 text-green-700' : 'bg-secondary text-muted-foreground'}">
                {ev.status}
              </span>
            </div>
          {/each}
        </div>
      </div>
    {/if}

    <!-- Save -->
    <div class="flex justify-end pt-2">
      <div class="flex gap-2">
        <Button variant="outline" href="/admin/maintenances">Cancel</Button>
        <Button onclick={save} disabled={saving} class="gap-2">
          <SaveIcon class="size-4" />
          {saving ? "Saving…" : "Save Changes"}
        </Button>
      </div>
    </div>
  </div>
</div>

<!-- Cancel confirmation -->
<Dialog.Root bind:open={confirmCancel}>
  <Dialog.Content class="max-w-sm">
    <Dialog.Header>
      <Dialog.Title>Cancel Maintenance?</Dialog.Title>
      <Dialog.Description>
        This will cancel the maintenance window. This action cannot be undone.
      </Dialog.Description>
    </Dialog.Header>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (confirmCancel = false)}>Keep</Button>
      <Button variant="default" onclick={cancelMaintenance} class="bg-amber-600 hover:bg-amber-700">Cancel Maintenance</Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Delete confirmation -->
<Dialog.Root bind:open={confirmDelete}>
  <Dialog.Content class="max-w-sm">
    <Dialog.Header>
      <Dialog.Title>Delete Maintenance?</Dialog.Title>
      <Dialog.Description>
        This will permanently delete the maintenance and all its events. This action cannot be undone.
      </Dialog.Description>
    </Dialog.Header>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (confirmDelete = false)}>Keep</Button>
      <Button variant="destructive" onclick={deleteMaintenance}>Delete</Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
