<script lang="ts">
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import * as Dialog from "$lib/components/ui/dialog/index.js";
  import * as AlertDialog from "$lib/components/ui/alert-dialog/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { toast } from "svelte-sonner";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CopyIcon from "@lucide/svelte/icons/copy";
  import CheckIcon from "@lucide/svelte/icons/check";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import TrashIcon from "@lucide/svelte/icons/trash-2";
  import type { ApiKeyDto, ApiKeyCreatedResponse } from "$lib/api.js";

  let keys = $state<ApiKeyDto[]>([]);
  let loading = $state(true);
  let error = $state("");

  // Create dialog
  let createOpen = $state(false);
  let newKeyName = $state("");
  let creating = $state(false);
  let createdKey = $state<ApiKeyCreatedResponse | null>(null);
  let copied = $state(false);

  // Revoke dialog
  let revokeTarget = $state<ApiKeyDto | null>(null);
  let revoking = $state(false);

  async function load() {
    loading = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getApiKeys" }),
      });
      const result = await res.json();
      if (result.error) { error = result.error; return; }
      keys = result;
    } catch { error = "Failed to load API keys."; }
    finally { loading = false; }
  }

  async function createKey() {
    if (!newKeyName.trim()) return;
    creating = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "createApiKey", data: { name: newKeyName.trim() } }),
      });
      const result = await res.json();
      if (result.error) { toast.error(result.error); return; }
      createdKey = result;
      keys = [{ id: result.id, name: result.name, maskedKey: result.maskedKey, status: "ACTIVE", createdAt: result.createdAt }, ...keys];
    } catch { toast.error("Failed to create API key."); }
    finally { creating = false; }
  }

  async function revokeKey() {
    if (!revokeTarget) return;
    revoking = true;
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "revokeApiKey", data: { id: revokeTarget.id } }),
      });
      const result = await res.json();
      if (result.error) { toast.error(result.error); return; }
      keys = keys.map(k => k.id === revokeTarget!.id ? { ...k, status: "REVOKED" } : k);
      toast.success(`"${revokeTarget.name}" revoked.`);
      revokeTarget = null;
    } catch { toast.error("Failed to revoke key."); }
    finally { revoking = false; }
  }

  async function copyKey() {
    if (!createdKey) return;
    await navigator.clipboard.writeText(createdKey.rawKey);
    copied = true;
    setTimeout(() => copied = false, 2000);
  }

  function closeCreateDialog() {
    createOpen = false;
    createdKey = null;
    newKeyName = "";
    copied = false;
  }

  $effect(() => {
    load();
  });
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div class="flex items-center justify-between">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold">API Keys</h1>
      <p class="text-sm text-muted-foreground">Manage API keys for programmatic access to the Piro API.</p>
    </div>
    <Button onclick={() => { createOpen = true; newKeyName = ""; createdKey = null; }}>
      <PlusIcon class="size-4 mr-1" /> New API Key
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
    {:else if keys.length === 0}
      <Card.Content class="py-12 text-center text-sm text-muted-foreground">
        No API keys yet. Create one to get started.
      </Card.Content>
    {:else}
      <div class="divide-y">
        {#each keys as key (key.id)}
          <div class="flex items-center gap-4 px-6 py-4">
            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium truncate">{key.name}</p>
              <p class="text-xs font-mono text-muted-foreground mt-0.5">{key.maskedKey}</p>
            </div>
            <div class="flex items-center gap-3 shrink-0">
              <Badge variant={key.status === "ACTIVE" ? "default" : "secondary"}>
                {key.status}
              </Badge>
              <p class="text-xs text-muted-foreground">
                {new Date(key.createdAt).toLocaleDateString()}
              </p>
              {#if key.status === "ACTIVE"}
                <Button
                  variant="ghost"
                  size="icon"
                  class="size-8 text-muted-foreground hover:text-destructive"
                  onclick={() => revokeTarget = key}
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

<!-- Create dialog -->
<Dialog.Root bind:open={createOpen} onOpenChange={(open) => { if (!open) closeCreateDialog(); }}>
  <Dialog.Content class="sm:max-w-md">
    <Dialog.Header>
      <Dialog.Title>New API Key</Dialog.Title>
      <Dialog.Description>
        {#if createdKey}
          Copy your API key now — it won't be shown again.
        {:else}
          Give your key a descriptive name.
        {/if}
      </Dialog.Description>
    </Dialog.Header>

    {#if createdKey}
      <!-- Show raw key -->
      <div class="space-y-3">
        <div class="rounded-lg border bg-muted/40 p-4 space-y-2">
          <p class="text-xs text-muted-foreground">Your new API key:</p>
          <div class="flex items-center gap-2">
            <code class="text-sm font-mono flex-1 break-all">{createdKey.rawKey}</code>
            <Button variant="outline" size="icon" class="size-8 shrink-0" onclick={copyKey}>
              {#if copied}
                <CheckIcon class="size-4 text-green-600" />
              {:else}
                <CopyIcon class="size-4" />
              {/if}
            </Button>
          </div>
        </div>
        <Alert.Root class="border-amber-200 bg-amber-50 text-amber-800 dark:bg-amber-950 dark:border-amber-800 dark:text-amber-300">
          <AlertCircleIcon />
          <Alert.Description class="text-xs">
            Store this key securely. You won't be able to see it again.
          </Alert.Description>
        </Alert.Root>
      </div>
      <Dialog.Footer>
        <Button onclick={closeCreateDialog}>Done</Button>
      </Dialog.Footer>
    {:else}
      <!-- Create form -->
      <div class="space-y-2">
        <Label for="key-name">Name</Label>
        <Input
          id="key-name"
          placeholder="e.g. CI/CD Pipeline"
          bind:value={newKeyName}
          onkeydown={(e) => { if (e.key === "Enter" && newKeyName.trim()) createKey(); }}
        />
      </div>
      <Dialog.Footer>
        <Button variant="outline" onclick={closeCreateDialog}>Cancel</Button>
        <Button onclick={createKey} disabled={creating || !newKeyName.trim()}>
          {#if creating}<Spinner class="size-4 mr-1" />{/if}
          Create
        </Button>
      </Dialog.Footer>
    {/if}
  </Dialog.Content>
</Dialog.Root>

<!-- Revoke confirmation -->
<AlertDialog.Root open={!!revokeTarget} onOpenChange={(open) => { if (!open) revokeTarget = null; }}>
  <AlertDialog.Content>
    <AlertDialog.Header>
      <AlertDialog.Title>Revoke API Key</AlertDialog.Title>
      <AlertDialog.Description>
        Are you sure you want to revoke <strong>"{revokeTarget?.name}"</strong>?
        Any application using this key will lose access immediately.
      </AlertDialog.Description>
    </AlertDialog.Header>
    <AlertDialog.Footer>
      <AlertDialog.Cancel onclick={() => revokeTarget = null}>Cancel</AlertDialog.Cancel>
      <AlertDialog.Action
        class="bg-destructive text-white hover:bg-destructive/90"
        onclick={revokeKey}
        disabled={revoking}
      >
        {#if revoking}<Spinner class="size-4 mr-1" />{/if}
        Revoke
      </AlertDialog.Action>
    </AlertDialog.Footer>
  </AlertDialog.Content>
</AlertDialog.Root>
