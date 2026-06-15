<script lang="ts">
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Separator } from "$lib/components/ui/separator/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { toast } from "svelte-sonner";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CopyIcon from "@lucide/svelte/icons/copy";
  import CheckIcon from "@lucide/svelte/icons/check";
  import ChevronLeftIcon from "@lucide/svelte/icons/chevron-left";
  import type { WorkerCreatedResponse } from "$lib/api.js";

  // Step 1 — form
  let name = $state("");
  let region = $state("default");
  let creating = $state(false);
  let formError = $state("");

  // Step 2 — token reveal
  let created = $state<WorkerCreatedResponse | null>(null);
  let copiedToken = $state(false);
  let copiedEnv = $state(false);
  let copiedDockerEnv = $state(false);

  async function register() {
    if (!name.trim()) return;
    creating = true; formError = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "createWorker",
          data: { name: name.trim(), region: region.trim() || "default" },
        }),
      });
      const result = await res.json();
      if (result.error) { formError = result.error; return; }
      created = result;
    } catch { formError = "Failed to register worker. Check the API connection."; }
    finally { creating = false; }
  }

  async function copy(text: string, flag: "token" | "env" | "docker") {
    await navigator.clipboard.writeText(text);
    if (flag === "token") { copiedToken = true; setTimeout(() => copiedToken = false, 2000); }
    if (flag === "env")   { copiedEnv = true;   setTimeout(() => copiedEnv = false, 2000); }
    if (flag === "docker"){ copiedDockerEnv = true; setTimeout(() => copiedDockerEnv = false, 2000); }
  }

  const apiUrl = typeof window !== "undefined" ? window.location.origin.replace(/:\d+$/, ":5117") : "http://localhost:5117";
  let envBlock = $derived(created
    ? `PIRO_API_URL=${apiUrl}\nPIRO_WORKER_TOKEN=${created.workerToken}`
    : "");
  let dockerBlock = $derived(created
    ? `docker run -d \\\n  -e PIRO_API_URL=${apiUrl} \\\n  -e PIRO_WORKER_TOKEN=${created.workerToken} \\\n  ghcr.io/heva-co/piro-worker:latest`
    : "");
</script>


<div class="flex w-full flex-col gap-6 p-4 max-w-3xl">

  <!-- Breadcrumb -->
  <div class="flex items-center gap-1 text-sm text-muted-foreground">
    <button class="hover:text-foreground flex items-center gap-1" onclick={() => goto("/admin/configuration/workers")}>
      <ChevronLeftIcon class="size-4" /> Workers
    </button>
    <span>/</span>
    <span class="text-foreground font-medium">Register new worker</span>
  </div>

  {#if !created}
    <!-- ── Step 1: Form ──────────────────────────────────────────────────── -->
    <div class="space-y-1">
      <h1 class="text-xl font-semibold">Register a new worker</h1>
      <p class="text-sm text-muted-foreground">
        Workers run check executors in different regions and report results back to this API.
        After registration you'll receive a one-time token to configure the worker process.
      </p>
    </div>

    <Separator />

    <div class="space-y-4 max-w-sm">
      {#if formError}
        <Alert.Root variant="destructive">
          <AlertCircleIcon />
          <Alert.Description>{formError}</Alert.Description>
        </Alert.Root>
      {/if}

      <div class="space-y-2">
        <Label for="worker-name">Worker name</Label>
        <Input
          id="worker-name"
          placeholder="e.g. eu-west-1"
          bind:value={name}
          onkeydown={(e) => { if (e.key === "Enter" && name.trim()) register(); }}
        />
        <p class="text-xs text-muted-foreground">A descriptive label. Can be the region or datacenter name.</p>
      </div>

      <div class="space-y-2">
        <Label for="worker-region">Region label</Label>
        <Input
          id="worker-region"
          placeholder="default"
          bind:value={region}
          onkeydown={(e) => { if (e.key === "Enter" && name.trim()) register(); }}
        />
        <p class="text-xs text-muted-foreground">
          Used to tag check results (e.g. <code class="font-mono">eu-west-1</code>, <code class="font-mono">us-east-1</code>).
          Defaults to <code class="font-mono">default</code>.
        </p>
      </div>

      <div class="flex gap-2 pt-2">
        <Button variant="outline" onclick={() => goto("/admin/configuration/workers")}>Cancel</Button>
        <Button onclick={register} disabled={creating || !name.trim()}>
          {#if creating}<Spinner class="size-4 mr-1" />{/if}
          Register worker
        </Button>
      </div>
    </div>

  {:else}
    <!-- ── Step 2: Token reveal ──────────────────────────────────────────── -->
    <div class="space-y-1">
      <h1 class="text-xl font-semibold">Worker registered</h1>
      <p class="text-sm text-muted-foreground">
        Copy the registration token below and use it to start the <code class="font-mono text-xs">Piro.Worker</code> process.
        The token will not be shown again.
      </p>
    </div>

    <Alert.Root class="border-amber-200 bg-amber-50 text-amber-800 dark:bg-amber-950 dark:border-amber-800 dark:text-amber-300">
      <AlertCircleIcon />
      <Alert.Description class="text-xs">
        Store this token securely. You won't be able to see it again after leaving this page.
      </Alert.Description>
    </Alert.Root>

    <Separator />

    <!-- Registration token -->
    <div class="space-y-3">
      <div>
        <p class="text-sm font-medium">Registration token</p>
        <p class="text-xs text-muted-foreground mt-0.5">
          Worker: <strong>{created.name}</strong> &nbsp;·&nbsp; Region: <code class="font-mono">{created.region}</code>
        </p>
      </div>

      <div class="rounded-lg border bg-muted/40 p-4 space-y-2">
        <p class="text-xs text-muted-foreground">PIRO_WORKER_TOKEN</p>
        <div class="flex items-center gap-2">
          <code class="text-sm font-mono flex-1 break-all select-all">{created.workerToken}</code>
          <Button variant="outline" size="icon" class="size-8 shrink-0" onclick={() => copy(created!.workerToken, "token")}>
            {#if copiedToken}
              <CheckIcon class="size-4 text-green-600" />
            {:else}
              <CopyIcon class="size-4" />
            {/if}
          </Button>
        </div>
      </div>
    </div>

    <Separator />

    <!-- Environment variables -->
    <div class="space-y-3">
      <p class="text-sm font-medium">Configure with environment variables</p>
      <div class="rounded-lg border bg-muted/50 p-4">
        <div class="flex items-start justify-between gap-2">
          <pre class="text-xs font-mono text-foreground whitespace-pre-wrap flex-1 leading-relaxed">{envBlock}</pre>
          <Button variant="ghost" size="icon" class="size-7 shrink-0 mt-0.5" onclick={() => copy(envBlock, "env")}>
            {#if copiedEnv}
              <CheckIcon class="size-3.5 text-green-600" />
            {:else}
              <CopyIcon class="size-3.5" />
            {/if}
          </Button>
        </div>
      </div>
    </div>

    <!-- Docker run -->
    <div class="space-y-3">
      <p class="text-sm font-medium">Run with Docker</p>
      <div class="rounded-lg border bg-muted/50 p-4">
        <div class="flex items-start justify-between gap-2">
          <pre class="text-xs font-mono text-foreground whitespace-pre-wrap flex-1 leading-relaxed">{dockerBlock}</pre>
          <Button variant="ghost" size="icon" class="size-7 shrink-0 mt-0.5" onclick={() => copy(dockerBlock, "docker")}>
            {#if copiedDockerEnv}
              <CheckIcon class="size-3.5 text-green-600" />
            {:else}
              <CopyIcon class="size-3.5" />
            {/if}
          </Button>
        </div>
      </div>
    </div>

    <div class="pt-2">
      <Button onclick={() => goto("/admin/configuration/workers")}>
        Back to workers
      </Button>
    </div>
  {/if}
</div>
