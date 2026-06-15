<script lang="ts">
  import type { PageData, ActionData } from "./$types";
  import { formatStatus } from "$lib/api.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Accordion from "$lib/components/ui/accordion/index.js";
  import * as Table from "$lib/components/ui/table/index.js";
  import * as Tooltip from "$lib/components/ui/tooltip/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import * as Switch from "$lib/components/ui/switch/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import SaveIcon from "@lucide/svelte/icons/save";
  import Trash2Icon from "@lucide/svelte/icons/trash-2";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CheckCircleIcon from "@lucide/svelte/icons/check-circle";
  import LoaderIcon from "@lucide/svelte/icons/loader";

  let { data, form }: { data: PageData; form: ActionData } = $props();

  let svcName = $state(data.service.name);
  let svcDisplayOrder = $state(data.service.displayOrder);
  let svcDescription = $state(data.service.description ?? "");
  let svcIsHidden = $state(data.service.isHidden);
  let svcHistoryDesktop = $state(data.service.historyDaysDesktop);
  let svcHistoryMobile = $state(data.service.historyDaysMobile);

  const isDirty = $derived(
    svcName !== data.service.name ||
    svcDisplayOrder !== data.service.displayOrder ||
    svcDescription !== (data.service.description ?? "") ||
    svcIsHidden !== data.service.isHidden
  );
  const isHistoryDirty = $derived(
    svcHistoryDesktop !== data.service.historyDaysDesktop ||
    svcHistoryMobile !== data.service.historyDaysMobile
  );

  const DEFAULT_STATUSES = ["NO_DATA", "UP", "DOWN", "DEGRADED"];
  let activeSection = $state("general");

  const statusVariant = (s: string) =>
    s === "UP" ? "default" : s === "DOWN" ? "destructive" : "secondary";
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <!-- Header -->
  <div class="flex items-center justify-between">
    <div class="flex items-center gap-2 text-sm text-muted-foreground">
      <a href="/admin/services" class="hover:text-foreground transition-colors">Services</a>
      <span>/</span>
      <span class="text-foreground font-medium">{data.service.name}</span>
    </div>
    <div class="flex items-center gap-2">
      <Badge variant={statusVariant(data.service.currentStatus)}>{formatStatus(data.service.currentStatus)}</Badge>
      <Button size="sm" variant="outline" href="/" target="_blank">View</Button>
    </div>
  </div>

  <!-- Alerts -->
  {#if form?.error}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Title>Error</Alert.Title>
      <Alert.Description>{form.error}</Alert.Description>
    </Alert.Root>
  {/if}
  {#if form?.success}
    <Alert.Root class="border-green-200 bg-green-50 text-green-800 dark:bg-green-950 dark:border-green-800 dark:text-green-300">
      <CheckCircleIcon />
      <Alert.Title>Saved</Alert.Title>
      <Alert.Description>Service updated successfully.</Alert.Description>
    </Alert.Root>
  {/if}

  <Accordion.Root type="single" class="w-full" bind:value={activeSection}>

    <!-- General Settings -->
    <Accordion.Item value="general">
      <Accordion.Trigger>General Settings</Accordion.Trigger>
      <Accordion.Content>
        <Card.Root>
          <Card.Content class="space-y-4 pt-4">
            <div class="grid grid-cols-2 gap-4">
              <div class="flex flex-col gap-2">
                <Label for="svc-name">Name <span class="text-destructive">*</span></Label>
                <Input id="svc-name" bind:value={svcName} placeholder="My Service" />
              </div>
              <div class="flex flex-col gap-2">
                <Label>Slug</Label>
                <Input value={data.service.slug} disabled class="font-mono opacity-60" />
                <p class="text-xs text-muted-foreground">Cannot be changed after creation</p>
              </div>
            </div>
            <div class="flex flex-col gap-2">
              <Label for="svc-desc">Description</Label>
              <Textarea id="svc-desc" bind:value={svcDescription} placeholder="A brief description of this service" rows={3} />
            </div>
            <div class="grid grid-cols-2 gap-4">
              <div class="flex flex-col gap-2">
                <Label for="svc-order">Display Order</Label>
                <Input id="svc-order" type="number" bind:value={svcDisplayOrder} />
                <p class="text-xs text-muted-foreground">Lower numbers appear first</p>
              </div>
              <div class="flex flex-col gap-2">
                <Label>Hidden from Public Page</Label>
                <div class="flex items-center gap-3 pt-1">
                  <Switch.Root bind:checked={svcIsHidden} />
                  <span class="text-sm text-muted-foreground">{svcIsHidden ? "Hidden" : "Visible"}</span>
                </div>
                <p class="text-xs text-muted-foreground">Hidden services won't appear on the status page</p>
              </div>
            </div>
          </Card.Content>
          <Card.Footer class="flex justify-end">
            <form method="POST" action="?/updateService">
              <input type="hidden" name="name" value={svcName} />
              <input type="hidden" name="displayOrder" value={svcDisplayOrder} />
              <input type="hidden" name="description" value={svcDescription} />
              <input type="hidden" name="isHidden" value={svcIsHidden ? "on" : ""} />
              <Button type="submit" disabled={!isDirty}>
                <SaveIcon class="size-4" />
                Save changes
              </Button>
            </form>
          </Card.Footer>
        </Card.Root>
      </Accordion.Content>
    </Accordion.Item>

    <!-- Checks -->
    <Accordion.Item value="checks">
      <Accordion.Trigger>Checks ({data.checks.length})</Accordion.Trigger>
      <Accordion.Content>
        <Card.Root>
          <Card.Header class="flex flex-row items-center justify-between pb-2">
            <Card.Description>Monitoring probes configured for this service</Card.Description>
            <Button size="sm" href="/admin/services/{data.service.slug}/checks">
              <PlusIcon class="size-4 mr-1" /> Add check
            </Button>
          </Card.Header>
          <Card.Content class="p-0">
            {#if data.checks.length === 0}
              <div class="px-6 py-10 text-center text-sm text-muted-foreground">
                No checks yet. Add one to start monitoring.
              </div>
            {:else}
              <div class="ktable">
                <Table.Root>
                  <Table.Header>
                    <Table.Row>
                      <Table.Head>Name</Table.Head>
                      <Table.Head class="w-24">Type</Table.Head>
                      <Table.Head class="w-36">Schedule</Table.Head>
                      <Table.Head class="w-28">Status</Table.Head>
                      <Table.Head class="w-28 text-right"></Table.Head>
                    </Table.Row>
                  </Table.Header>
                  <Table.Body>
                    {#each data.checks as check (check.id)}
                      <Table.Row>
                        <Table.Cell>
                          <div class="font-medium">{check.name}</div>
                          <div class="text-xs text-muted-foreground font-mono">{check.slug}</div>
                        </Table.Cell>
                        <Table.Cell>
                          <Badge variant="secondary">{check.type}</Badge>
                        </Table.Cell>
                        <Table.Cell>
                          <Tooltip.Root>
                            <Tooltip.Trigger>
                              <span class="text-xs text-muted-foreground font-mono">{check.cron}</span>
                            </Tooltip.Trigger>
                            <Tooltip.Content>Cron expression</Tooltip.Content>
                          </Tooltip.Root>
                        </Table.Cell>
                        <Table.Cell>
                          <Badge variant={statusVariant(check.currentStatus)}>{formatStatus(check.currentStatus)}</Badge>
                        </Table.Cell>
                        <Table.Cell class="text-right">
                          <Button variant="outline" size="sm" href="/admin/services/{data.service.slug}/checks/{check.slug}">
                            Configure
                          </Button>
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

    <!-- Uptime -->
    <Accordion.Item value="uptime">
      <Accordion.Trigger>Uptime</Accordion.Trigger>
      <Accordion.Content>
        <Card.Root>
          <Card.Content class="pt-4">
            <p class="text-sm text-muted-foreground">
              Uptime statistics are computed from the service status history.
              The service status is derived from its checks and dependency propagation.
            </p>
            <div class="mt-4 grid grid-cols-3 gap-4">
              <div class="rounded-lg border p-4 text-center">
                <div class="text-2xl font-bold">—</div>
                <div class="text-xs text-muted-foreground mt-1">Last 24h</div>
              </div>
              <div class="rounded-lg border p-4 text-center">
                <div class="text-2xl font-bold">—</div>
                <div class="text-xs text-muted-foreground mt-1">Last 7 days</div>
              </div>
              <div class="rounded-lg border p-4 text-center">
                <div class="text-2xl font-bold">—</div>
                <div class="text-xs text-muted-foreground mt-1">Last 30 days</div>
              </div>
            </div>
          </Card.Content>
        </Card.Root>
      </Accordion.Content>
    </Accordion.Item>

    <!-- Status History Days -->
    <Accordion.Item value="history">
      <Accordion.Trigger>Status History</Accordion.Trigger>
      <Accordion.Content>
        <Card.Root>
          <Card.Content class="space-y-4 pt-4">
            <p class="text-sm text-muted-foreground">
              Configure how many days of status history to display on the status page.
              Values must be between 1 and 365.
            </p>
            <div class="grid grid-cols-2 gap-4">
              <div class="flex flex-col gap-2">
                <Label for="hist-desktop">Desktop (days)</Label>
                <Input id="hist-desktop" type="number" min="1" max="365" placeholder="30" bind:value={svcHistoryDesktop} />
              </div>
              <div class="flex flex-col gap-2">
                <Label for="hist-mobile">Mobile (days)</Label>
                <Input id="hist-mobile" type="number" min="1" max="365" placeholder="15" bind:value={svcHistoryMobile} />
              </div>
            </div>
          </Card.Content>
          <Card.Footer class="flex justify-end">
            <form method="POST" action="?/updateHistoryDays">
              <input type="hidden" name="historyDaysDesktop" value={svcHistoryDesktop} />
              <input type="hidden" name="historyDaysMobile" value={svcHistoryMobile} />
              <Button type="submit" disabled={!isHistoryDirty}>
                <SaveIcon class="size-4" />
                Save history settings
              </Button>
            </form>
          </Card.Footer>
        </Card.Root>
      </Accordion.Content>
    </Accordion.Item>

    <!-- Danger Zone -->
    <Accordion.Item value="danger">
      <Accordion.Trigger class="text-destructive hover:text-destructive">Danger Zone</Accordion.Trigger>
      <Accordion.Content>
        <Card.Root class="border-destructive/50">
          <Card.Content class="pt-4">
            <div class="flex items-center justify-between">
              <div>
                <p class="font-medium text-sm">Delete this service</p>
                <p class="text-xs text-muted-foreground mt-1">
                  Permanently deletes the service, all its checks, and historical data. This cannot be undone.
                </p>
              </div>
              <form method="POST" action="?/deleteService">
                <Button type="submit" variant="destructive" size="sm"
                  onclick={(e) => { if (!confirm("Delete this service and all its checks?")) e.preventDefault(); }}>
                  <Trash2Icon class="size-4 mr-1" /> Delete service
                </Button>
              </form>
            </div>
          </Card.Content>
        </Card.Root>
      </Accordion.Content>
    </Accordion.Item>

  </Accordion.Root>
</div>
