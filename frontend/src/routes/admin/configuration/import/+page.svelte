<script lang="ts">
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CheckCircleIcon from "@lucide/svelte/icons/check-circle";
  import UploadIcon from "@lucide/svelte/icons/upload";
  import FileIcon from "@lucide/svelte/icons/file";
  import HelpCircleIcon from "@lucide/svelte/icons/help-circle";
  import type { ImportPlanDto, ImportPlanEntryDto } from "$lib/api.js";

  let dragging = $state(false);
  let fileName = $state("");
  let yamlContent = $state("");
  let planning = $state(false);
  let applying = $state(false);
  let plan = $state<ImportPlanDto | null>(null);
  let applied = $state(false);
  let error = $state("");

  function actionVariant(action: ImportPlanEntryDto["action"]) {
    if (action === "Create") return "default";
    if (action === "Update") return "secondary";
    return "outline";
  }

  function actionColor(action: ImportPlanEntryDto["action"]) {
    if (action === "Create") return "bg-green-500/15 text-green-700 dark:text-green-400 border-green-300 dark:border-green-800";
    if (action === "Update") return "bg-blue-500/15 text-blue-700 dark:text-blue-400 border-blue-300 dark:border-blue-800";
    return "bg-muted text-muted-foreground";
  }

  async function loadFile(file: File) {
    if (!file.name.endsWith(".yaml") && !file.name.endsWith(".yml")) {
      error = "Only .yaml or .yml files are supported.";
      return;
    }
    fileName = file.name;
    yamlContent = await file.text();
    plan = null;
    applied = false;
    error = "";
    await runPlan();
  }

  async function runPlan() {
    if (!yamlContent) return;
    planning = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "importConfigPlan", data: { yaml: yamlContent } }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      plan = result;
    } catch { error = "Failed to generate import plan."; }
    finally { planning = false; }
  }

  async function applyPlan() {
    applying = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "importConfigApply", data: { yaml: yamlContent } }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      plan = result;
      applied = true;
    } catch { error = "Failed to apply import."; }
    finally { applying = false; }
  }

  function onFileInput(e: Event) {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (file) loadFile(file);
  }

  function onDrop(e: DragEvent) {
    e.preventDefault(); dragging = false;
    const file = e.dataTransfer?.files?.[0];
    if (file) loadFile(file);
  }
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div class="flex items-center justify-between">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold">Import Configuration</h1>
      <p class="text-sm text-muted-foreground">Upload a <code class="bg-muted px-1 rounded text-xs">piro.yaml</code> file to create or update services, checks, triggers, and alert configs.</p>
    </div>
    <Button variant="outline" size="sm" href="https://github.com/Heva-Co/piro/wiki/Configuration-as-Code" target="_blank" rel="noopener noreferrer">
      <HelpCircleIcon class="size-4 mr-1" /> Help
    </Button>
  </div>

  {#if error}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Description>{error}</Alert.Description>
    </Alert.Root>
  {/if}

  {#if applied && plan && !plan.hasErrors}
    <Alert.Root class="border-green-200 bg-green-50 text-green-800 dark:bg-green-950 dark:border-green-800 dark:text-green-300">
      <CheckCircleIcon />
      <Alert.Description>
        Import applied — {plan.created} created, {plan.updated} updated, {plan.skipped} skipped.
      </Alert.Description>
    </Alert.Root>
  {/if}

  <!-- Drop zone -->
  <Card.Root
    class="border-2 border-dashed transition-colors cursor-pointer {dragging ? 'border-primary bg-primary/5' : 'border-border hover:border-primary/50'}"
    ondragover={(e) => { e.preventDefault(); dragging = true; }}
    ondragleave={() => dragging = false}
    ondrop={onDrop}
  >
    <Card.Content class="flex flex-col items-center justify-center py-12 gap-3">
      {#if fileName}
        <FileIcon class="size-8 text-muted-foreground" />
        <p class="text-sm font-medium">{fileName}</p>
        <p class="text-xs text-muted-foreground">Drop a new file or click below to replace</p>
      {:else}
        <UploadIcon class="size-8 text-muted-foreground" />
        <p class="text-sm font-medium">Drop your <code class="bg-muted px-1 rounded">piro.yaml</code> here</p>
        <p class="text-xs text-muted-foreground">or click to browse</p>
      {/if}
      <label class="cursor-pointer">
        <input type="file" accept=".yaml,.yml" class="sr-only" onchange={onFileInput} />
        <Button variant="outline" size="sm" onclick={() => {}}>Browse file</Button>
      </label>
    </Card.Content>
  </Card.Root>

  <!-- Plan preview -->
  {#if planning}
    <div class="flex items-center gap-2 py-4 text-muted-foreground text-sm">
      <Spinner class="size-4" /> Generating plan…
    </div>
  {:else if plan}
    <Card.Root>
      <Card.Header>
        <div class="flex items-center justify-between">
          <div>
            <Card.Title>Import Plan</Card.Title>
            <Card.Description class="mt-1">
              {plan.created} to create · {plan.updated} to update · {plan.skipped} unchanged
            </Card.Description>
          </div>
          {#if !applied}
            <Button onclick={applyPlan} disabled={applying || plan.hasErrors}>
              {#if applying}<Spinner class="size-4 mr-1" />{/if}
              Confirm & Apply
            </Button>
          {/if}
        </div>
      </Card.Header>

      {#if plan.errors.length > 0}
        <Card.Content class="pt-0">
          <div class="rounded-lg border border-destructive/50 bg-destructive/5 p-4 space-y-1">
            <p class="text-sm font-medium text-destructive">Errors — fix these before applying:</p>
            {#each plan.errors as e (e)}
              <p class="text-xs text-destructive">• {e}</p>
            {/each}
          </div>
        </Card.Content>
      {/if}

      {#if plan.entries.length > 0}
        <Card.Content class="pt-0 space-y-3">
          {@const triggers = plan.entries.filter(e => e.entityType === "Trigger")}
          {@const services = plan.entries.filter(e => e.entityType === "Service")}
          {@const checks = plan.entries.filter(e => e.entityType === "Check")}
          {@const alerts = plan.entries.filter(e => e.entityType === "Alert")}

          <!-- Triggers -->
          {#if triggers.length > 0}
            <div class="rounded-lg border overflow-hidden">
              <div class="bg-muted/40 px-4 py-2 border-b">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Triggers</p>
              </div>
              <div class="divide-y">
                {#each triggers as entry (entry.name)}
                  <div class="flex items-center gap-3 px-4 py-2.5 {entry.action === 'Skip' ? 'opacity-50' : ''}">
                    <span class="inline-flex shrink-0 items-center rounded-full border px-2 py-0.5 text-xs font-medium {actionColor(entry.action)}">{entry.action}</span>
                    <span class="text-sm font-medium flex-1">{entry.name}</span>
                    <span class="text-xs text-muted-foreground">{entry.details ?? ""}</span>
                  </div>
                {/each}
              </div>
            </div>
          {/if}

          <!-- Services tree -->
          {#if services.length > 0}
            <div class="rounded-lg border overflow-hidden">
              <div class="bg-muted/40 px-4 py-2 border-b">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Services & Checks</p>
              </div>
              <div class="divide-y">
                {#each services as svc (svc.slug)}
                  {@const svcChecks = checks.filter(c => c.parentSlug === svc.slug)}
                  <!-- Service row -->
                  <div class="flex items-center gap-3 px-4 py-2.5 {svc.action === 'Skip' ? 'opacity-50' : ''}">
                    <span class="inline-flex shrink-0 items-center rounded-full border px-2 py-0.5 text-xs font-medium {actionColor(svc.action)}">{svc.action}</span>
                    <span class="text-sm font-semibold flex-1">{svc.name}</span>
                    <span class="font-mono text-xs text-muted-foreground">{svc.slug ?? ""}</span>
                  </div>
                  <!-- Check rows indented -->
                  {#each svcChecks as chk (chk.slug + svc.slug)}
                    {@const chkAlerts = alerts.filter(a => a.parentSlug === `${svc.slug}/${chk.slug}`)}
                    <div class="flex items-center gap-3 px-4 py-2 pl-10 bg-muted/20 {chk.action === 'Skip' ? 'opacity-50' : ''}">
                      <span class="text-muted-foreground text-xs select-none mr-1">└</span>
                      <span class="inline-flex shrink-0 items-center rounded-full border px-2 py-0.5 text-xs font-medium {actionColor(chk.action)}">{chk.action}</span>
                      <span class="text-sm flex-1">{chk.name}</span>
                      <span class="font-mono text-xs text-muted-foreground mr-4">{chk.slug ?? ""}</span>
                      <span class="text-xs text-muted-foreground">{chk.details ?? ""}</span>
                    </div>
                    <!-- Alert rows double-indented -->
                    {#each chkAlerts as alt (alt.name)}
                      <div class="flex items-center gap-3 px-4 py-2 pl-16 bg-muted/10 {alt.action === 'Skip' ? 'opacity-50' : ''}">
                        <span class="text-muted-foreground text-xs select-none mr-1">└</span>
                        <span class="inline-flex shrink-0 items-center rounded-full border px-2 py-0.5 text-xs font-medium {actionColor(alt.action)}">{alt.action}</span>
                        <span class="text-sm flex-1 text-muted-foreground">Alert: {alt.name}</span>
                        <span class="text-xs text-muted-foreground">{alt.details ?? ""}</span>
                      </div>
                    {/each}
                  {/each}
                {/each}
              </div>
            </div>
          {/if}
        </Card.Content>
      {:else if plan.errors.length === 0}
        <Card.Content class="pt-0">
          <p class="text-sm text-muted-foreground py-4 text-center">Nothing to import — all entities are already up to date.</p>
        </Card.Content>
      {/if}
    </Card.Root>
  {/if}
</div>
