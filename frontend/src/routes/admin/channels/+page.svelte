<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Table from "$lib/components/ui/table/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import SettingsIcon from "@lucide/svelte/icons/settings";
  import GlobeIcon from "@lucide/svelte/icons/globe";
  import LockIcon from "@lucide/svelte/icons/lock";
  import TriangleAlertIcon from "@lucide/svelte/icons/triangle-alert";
  import type { NotificationChannelDto } from "$lib/api.js";

  const PAGE_SIZE = 10;

  let channels = $state<NotificationChannelDto[]>([]);
  let loading = $state(true);
  let error = $state("");
  let statusFilter = $state("all");
  let currentPage = $state(1);

  const filtered = $derived(
    statusFilter === "all"
      ? channels
      : channels.filter((t) => (statusFilter === "active" ? t.status !== "INACTIVE" : t.status === "INACTIVE"))
  );

  const totalPages = $derived(Math.max(1, Math.ceil(filtered.length / PAGE_SIZE)));
  const paged = $derived(filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE));

  $effect(() => { if (currentPage > totalPages) currentPage = 1; });

  function typeVariant(type: string): "default" | "secondary" | "outline" {
    if (type === "Email") return "default";
    if (type === "Webhook") return "secondary";
    return "outline";
  }

  async function load() {
    loading = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "getChannels", data: {} }),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else channels = result;
    } catch { error = "Failed to load notification channels."; }
    finally { loading = false; }
  }

  onMount(load);
</script>


<div class="flex w-full flex-col gap-6 p-4">
  <div class="flex items-center justify-between">
    <div>
      <h1 class="text-xl font-semibold">Notification Channels</h1>
      <p class="text-sm text-muted-foreground">Configure where alerts are sent when a check fails or recovers.</p>
    </div>
    <Button onclick={() => goto("/admin/channels/new")}>
      <PlusIcon class="size-4 mr-1" /> New Channel
    </Button>
  </div>

  {#if error}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Title>Error</Alert.Title>
      <Alert.Description>{error}</Alert.Description>
    </Alert.Root>
  {/if}

  <div class="flex items-center gap-3">
    <span class="text-sm text-muted-foreground">Status</span>
    <Select.Root type="single" value={statusFilter} onValueChange={(v) => v && (statusFilter = v)}>
      <Select.Trigger class="w-36">
        {statusFilter === "all" ? "All" : statusFilter === "active" ? "Active" : "Inactive"}
      </Select.Trigger>
      <Select.Content>
        <Select.Item value="all">All</Select.Item>
        <Select.Item value="active">Active</Select.Item>
        <Select.Item value="inactive">Inactive</Select.Item>
      </Select.Content>
    </Select.Root>
  </div>

  {#if loading}
    <div class="flex justify-center py-16"><Spinner class="size-6" /></div>
  {:else if channels.length === 0}
    <div class="rounded-lg border border-dashed flex flex-col items-center justify-center py-16 gap-3 text-center">
      <p class="text-muted-foreground text-sm">No notification channels yet.</p>
      <Button size="sm" onclick={() => goto("/admin/channels/new")}>
        <PlusIcon class="size-4 mr-1" /> Create your first channel
      </Button>
    </div>
  {:else}
    <div class="rounded-lg border">
      <Table.Root>
        <Table.Header>
          <Table.Row>
            <Table.Head>Name</Table.Head>
            <Table.Head>Type</Table.Head>
            <Table.Head>Status</Table.Head>
            <Table.Head></Table.Head>
            <Table.Head class="w-28"></Table.Head>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {#each paged as t (t.id)}
            <Table.Row>
              <Table.Cell class="font-medium">{t.name}</Table.Cell>
              <Table.Cell>
                <Badge variant={typeVariant(t.type)}>{t.type}</Badge>
              </Table.Cell>
              <Table.Cell>
                <Badge variant={t.status === "INACTIVE" ? "outline" : "secondary"}>
                  {t.status === "INACTIVE" ? "Inactive" : "Active"}
                </Badge>
              </Table.Cell>
              <Table.Cell>
                <div class="flex items-center gap-1.5">
                  {#if t.isGlobal}
                    <span class="inline-flex items-center gap-1 text-xs text-muted-foreground" title="Global — applied to all alert configs">
                      <GlobeIcon class="size-3.5" /> Global
                    </span>
                  {/if}
                  {#if t.isLocked}
                    <span class="inline-flex items-center gap-1 text-xs text-muted-foreground" title="Locked — cannot be removed from alert configs">
                      <LockIcon class="size-3.5" /> Locked
                    </span>
                  {/if}
                  {#if t.status !== "INACTIVE" && t.alertConfigCount === 0}
                    <span class="inline-flex items-center gap-1 text-xs text-amber-500" title="No alert configs linked — this channel won't fire">
                      <TriangleAlertIcon class="size-3.5" /> No alerts linked
                    </span>
                  {/if}
                </div>
              </Table.Cell>
              <Table.Cell>
                <Button variant="ghost" size="sm" onclick={() => goto(`/admin/channels/${t.id}`)}>
                  <SettingsIcon class="size-3.5 mr-1" /> Configure
                </Button>
              </Table.Cell>
            </Table.Row>
          {/each}
        </Table.Body>
      </Table.Root>
    </div>

    {#if totalPages > 1}
      <div class="flex items-center justify-end gap-2">
        <Button variant="outline" size="sm" disabled={currentPage === 1} onclick={() => currentPage--}>Previous</Button>
        {#each Array.from({ length: totalPages }, (_, i) => i + 1) as p (p)}
          <Button
            variant={currentPage === p ? "default" : "outline"}
            size="sm"
            onclick={() => currentPage = p}>{p}</Button>
        {/each}
        <Button variant="outline" size="sm" disabled={currentPage === totalPages} onclick={() => currentPage++}>Next</Button>
      </div>
    {/if}
  {/if}
</div>
