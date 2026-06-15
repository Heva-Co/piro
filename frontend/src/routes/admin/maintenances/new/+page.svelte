<script lang="ts">
  import type { PageData } from "./$types";
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Switch } from "$lib/components/ui/switch/index.js";
  import * as Breadcrumb from "$lib/components/ui/breadcrumb/index.js";
  import CalendarIcon from "lucide-svelte/icons/calendar";
  import RefreshCwIcon from "lucide-svelte/icons/refresh-cw";
  import InfoIcon from "lucide-svelte/icons/info";
  import CalendarPlusIcon from "lucide-svelte/icons/calendar-plus";

  let { data }: { data: PageData } = $props();

  // ── Schedule type ───────────────────────────────────────────────────────────
  type ScheduleType = "one-time" | "recurring";
  let scheduleType = $state<ScheduleType>("one-time");

  // ── Form state ──────────────────────────────────────────────────────────────
  let title = $state("");
  let description = $state("");
  let isGlobal = $state(false);

  // Default start = now rounded to next minute
  const nowLocal = (() => {
    const d = new Date();
    d.setSeconds(0, 0);
    d.setMinutes(d.getMinutes() + 1);
    return d.toISOString().slice(0, 16);
  })();
  let startDateTime = $state(nowLocal);

  // Duration
  let durationHours = $state(1);
  let durationMinutes = $state(0);
  const durationSeconds = $derived(durationHours * 3600 + durationMinutes * 60);

  // Recurring fields
  let recurringFreq = $state<"DAILY" | "WEEKLY" | "MONTHLY">("WEEKLY");
  let recurringInterval = $state(1);
  let recurringDays = $state<string[]>([]); // MON, TUE, WED ...
  const DAYS = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"] as const;
  const DAY_LABELS: Record<string, string> = { MO: "Mon", TU: "Tue", WE: "Wed", TH: "Thu", FR: "Fri", SA: "Sat", SU: "Sun" };

  function toggleDay(day: string) {
    if (recurringDays.includes(day)) recurringDays = recurringDays.filter(d => d !== day);
    else recurringDays = [...recurringDays, day];
  }

  // ── RRULE generation ────────────────────────────────────────────────────────
  const rrule = $derived.by(() => {
    if (scheduleType === "one-time") return "FREQ=MINUTELY;COUNT=1";
    let r = `FREQ=${recurringFreq};INTERVAL=${recurringInterval}`;
    if (recurringFreq === "WEEKLY" && recurringDays.length > 0)
      r += `;BYDAY=${recurringDays.join(",")}`;
    return r;
  });

  // ── Affected services ────────────────────────────────────────────────────────
  let selectedSlugs = $state<Set<string>>(new Set());
  function toggleService(slug: string) {
    const next = new Set(selectedSlugs);
    if (next.has(slug)) next.delete(slug); else next.add(slug);
    selectedSlugs = next;
  }

  // ── Submit ──────────────────────────────────────────────────────────────────
  let saving = $state(false);
  let error = $state("");

  async function create() {
    if (!title.trim()) { error = "Title is required."; return; }
    if (durationSeconds <= 0) { error = "Duration must be greater than 0."; return; }
    saving = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "createMaintenance",
          data: {
            title: title.trim(),
            description: description.trim() || undefined,
            startDateTime: Math.floor(new Date(startDateTime).getTime() / 1000),
            rRule: rrule,
            durationSeconds,
            isGlobal,
            serviceSlugs: isGlobal ? undefined : [...selectedSlugs],
          },
        }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      goto("/admin/maintenances");
    } catch { error = "Failed to create maintenance."; }
    finally { saving = false; }
  }
</script>


<div class="container mx-auto py-8 flex flex-col gap-6">
  <!-- Breadcrumb -->
  <Breadcrumb.Root>
    <Breadcrumb.List>
      <Breadcrumb.Item><Breadcrumb.Link href="/admin/maintenances">Maintenances</Breadcrumb.Link></Breadcrumb.Item>
      <Breadcrumb.Separator />
      <Breadcrumb.Item><Breadcrumb.Page>New Maintenance</Breadcrumb.Page></Breadcrumb.Item>
    </Breadcrumb.List>
  </Breadcrumb.Root>

  <!-- Form container -->
  <div class="rounded-2xl border bg-card p-6 flex flex-col gap-6">
    <!-- Header -->
    <div>
      <h1 class="text-xl font-semibold">Create New Maintenance</h1>
      <p class="text-sm text-muted-foreground mt-0.5">Schedule a new maintenance window using iCalendar RRULE format</p>
    </div>

    {#if error}
      <p class="text-destructive text-sm">{error}</p>
    {/if}

    <!-- Schedule Type -->
    <div class="flex flex-col gap-2">
      <label class="text-sm font-medium">Schedule Type <span class="text-destructive">*</span></label>
      <div class="flex gap-4">
        <label class="flex items-center gap-2 cursor-pointer text-sm">
          <input
            type="radio"
            name="scheduleType"
            value="one-time"
            checked={scheduleType === "one-time"}
            onchange={() => scheduleType = "one-time"}
            class="accent-primary"
          />
          <CalendarIcon class="size-4" />
          One-Time
        </label>
        <label class="flex items-center gap-2 cursor-pointer text-sm">
          <input
            type="radio"
            name="scheduleType"
            value="recurring"
            checked={scheduleType === "recurring"}
            onchange={() => scheduleType = "recurring"}
            class="accent-primary"
          />
          <RefreshCwIcon class="size-4" />
          Recurring
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
        <Input
          type="number"
          min={0}
          bind:value={durationHours}
          class="w-24"
        />
        <span class="text-sm text-muted-foreground">hours</span>
        <Input
          type="number"
          min={0}
          max={59}
          bind:value={durationMinutes}
          class="w-24"
        />
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
          <!-- Frequency -->
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-muted-foreground uppercase tracking-wide">Frequency</label>
            <select
              bind:value={recurringFreq}
              class="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="DAILY">Daily</option>
              <option value="WEEKLY">Weekly</option>
              <option value="MONTHLY">Monthly</option>
            </select>
          </div>
          <!-- Interval -->
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
                <button
                  type="button"
                  onclick={() => toggleDay(day)}
                  class="text-xs px-3 py-1.5 rounded-full border transition-colors {recurringDays.includes(day) ? 'bg-primary text-primary-foreground border-primary' : 'hover:bg-muted'}"
                >
                  {DAY_LABELS[day]}
                </button>
              {/each}
            </div>
          </div>
        {/if}
      {/if}

      <!-- RRULE preview -->
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
                  <input
                    type="checkbox"
                    checked={selectedSlugs.has(svc.slug)}
                    onchange={() => toggleService(svc.slug)}
                    class="rounded accent-primary"
                  />
                  {svc.name}
                </label>
              {/each}
            </div>
            <p class="text-xs text-muted-foreground mt-1">Select services and set their status during the maintenance window</p>
          </div>
        {/if}
      </div>
    {/if}

    <!-- Actions -->
    <div class="flex justify-end pt-2">
      <div class="flex gap-2">
        <Button variant="outline" href="/admin/maintenances">Cancel</Button>
        <Button onclick={create} disabled={saving} class="gap-2">
          <CalendarPlusIcon class="size-4" />
          {saving ? "Creating…" : "Create Maintenance"}
        </Button>
      </div>
    </div>
  </div>
</div>
