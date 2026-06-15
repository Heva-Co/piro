<script lang="ts">
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import * as AlertDialog from "$lib/components/ui/alert-dialog/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { toast } from "svelte-sonner";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import TrashIcon from "@lucide/svelte/icons/trash-2";
  import ServerIcon from "@lucide/svelte/icons/server";
  import type { WorkerDto } from "$lib/api.js";
  import { getAdminHub, stopAdminHub } from "$lib/adminHub.js";

  let { data } = $props();

  let workers = $state<WorkerDto[]>([]);
  let loading = $state(true);
  let error = $state("");
  let deleteTarget = $state<WorkerDto | null>(null);
  let deleting = $state(false);

  async function load() {
    loading = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getWorkers" }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      workers = result;
    } catch { error = "Failed to load workers."; }
    finally { loading = false; }
  }

  async function deleteWorker() {
    if (!deleteTarget) return;
    deleting = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "deleteWorker", data: { id: deleteTarget.id } }),
      });
      const result = await res.json();
      if (result.error) { toast.error(result.error); return; }
      workers = workers.filter(w => w.id !== deleteTarget!.id);
      toast.success(`Worker "${deleteTarget.name}" removed.`);
      deleteTarget = null;
    } catch { toast.error("Failed to remove worker."); }
    finally { deleting = false; }
  }

  let now = $state(Date.now());
  $effect(() => {
    const tick = setInterval(() => { now = Date.now(); }, 1_000);
    return () => clearInterval(tick);
  });

  function formatHeartbeat(ts: string | null): string {
    if (!ts) return "Never";
    const diff = now - new Date(ts).getTime();
    if (diff < 5_000) return "Just now";
    if (diff < 60_000) return `${Math.floor(diff / 1_000)}s ago`;
    if (diff < 3_600_000) return `${Math.floor(diff / 60_000)}m ago`;
    if (diff < 86_400_000) return `${Math.floor(diff / 3_600_000)}h ago`;
    return new Date(ts).toLocaleDateString();
  }


  async function tokenFactory(): Promise<string> {
    const res = await fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "getToken" }),
    });
    const result = await res.json();
    return result.token as string;
  }

  $effect(() => {
    load();

    let pollingInterval: ReturnType<typeof setInterval> | null = null;
    let hubCleanup = () => {};

    if (data.accessToken) {
      getAdminHub(tokenFactory).then((hub) => {
        hub.on("WorkersChanged", load);
        hub.onreconnected(() => load());
        hub.onclose(() => {
          // Hub permanently closed — fall back to polling
          pollingInterval = setInterval(load, 30_000);
        });
        hubCleanup = () => {
          hub.off("WorkersChanged", load);
          if (pollingInterval) clearInterval(pollingInterval);
        };
      }).catch(() => {
        pollingInterval = setInterval(load, 30_000);
        hubCleanup = () => { if (pollingInterval) clearInterval(pollingInterval); };
      });
    }

    return () => {
      hubCleanup();
      if (pollingInterval) clearInterval(pollingInterval);
    };
  });
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div class="flex items-center justify-between">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold">Workers</h1>
      <p class="text-sm text-muted-foreground">
        Remote check workers execute monitoring checks from different regions and report results back.
      </p>
    </div>
    <Button onclick={() => goto("/admin/configuration/workers/new")}>
      <PlusIcon class="size-4 mr-1" /> Register Worker
    </Button>
  </div>

  {#if error}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Description>{error}</Alert.Description>
    </Alert.Root>
  {/if}

  <Card.Root>
    {#if loading}
      <Card.Content class="flex justify-center py-12">
        <Spinner class="size-5 text-muted-foreground" />
      </Card.Content>
    {:else if workers.length === 0}
      <Card.Content class="py-16 flex flex-col items-center gap-3 text-center">
        <div class="rounded-full bg-muted p-4">
          <ServerIcon class="size-6 text-muted-foreground" />
        </div>
        <div>
          <p class="text-sm font-medium">No workers registered</p>
          <p class="text-xs text-muted-foreground mt-1">
            Register a worker to run checks from external regions.
          </p>
        </div>
        <Button variant="outline" size="sm" onclick={() => goto("/admin/configuration/workers/new")}>
          <PlusIcon class="size-4 mr-1" /> Register your first worker
        </Button>
      </Card.Content>
    {:else}
      <div class="divide-y">
        {#each workers as worker (worker.id)}
          <div class="flex items-center gap-4 px-6 py-4">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <p class="text-sm font-medium truncate">{worker.name}</p>
                <Badge variant={worker.isConnected ? "default" : "secondary"} class="text-xs">
                  {worker.isConnected ? "Online" : "Offline"}
                </Badge>
              </div>
              <p class="text-xs text-muted-foreground mt-0.5">
                Region: <span class="font-mono">{worker.region}</span>
                &nbsp;·&nbsp;
                Last heartbeat: {formatHeartbeat(worker.lastHeartbeat)}
                {#if worker.version}
                  &nbsp;·&nbsp; {worker.version}
                {/if}
              </p>
            </div>
            <div class="flex items-center gap-3 shrink-0">
              <p class="text-xs text-muted-foreground">
                {new Date(worker.createdAt).toLocaleDateString()}
              </p>
              {#if !worker.isBuiltIn}
                <Button
                  variant="ghost"
                  size="icon"
                  class="size-8 text-muted-foreground hover:text-destructive"
                  onclick={() => deleteTarget = worker}
                >
                  <TrashIcon class="size-4" />
                </Button>
              {/if}
            </div>
          </div>
        {/each}
      </div>
    {/if}
  </Card.Root>
</div>

<!-- Delete confirmation -->
<AlertDialog.Root open={!!deleteTarget} onOpenChange={(open) => { if (!open) deleteTarget = null; }}>
  <AlertDialog.Content>
    <AlertDialog.Header>
      <AlertDialog.Title>Remove Worker</AlertDialog.Title>
      <AlertDialog.Description>
        Are you sure you want to remove <strong>"{deleteTarget?.name}"</strong>?
        Any active connection from this worker will be terminated immediately.
      </AlertDialog.Description>
    </AlertDialog.Header>
    <AlertDialog.Footer>
      <AlertDialog.Cancel onclick={() => deleteTarget = null}>Cancel</AlertDialog.Cancel>
      <AlertDialog.Action
        class="bg-destructive text-white hover:bg-destructive/90"
        onclick={deleteWorker}
        disabled={deleting}
      >
        {#if deleting}<Spinner class="size-4 mr-1" />{/if}
        Remove
      </AlertDialog.Action>
    </AlertDialog.Footer>
  </AlertDialog.Content>
</AlertDialog.Root>
