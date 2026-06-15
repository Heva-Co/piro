<script lang="ts">
  import type { PageData } from "./$types";
  import StatusHeader from "$lib/components/StatusHeader.svelte";
  import ServiceRow from "$lib/components/ServiceRow.svelte";
  import IncidentCard from "$lib/components/IncidentCard.svelte";
  import MaintenanceCard from "$lib/components/MaintenanceCard.svelte";
  let { data }: { data: PageData } = $props();
</script>

<svelte:head>
  <title>Status Page</title>
  <meta name="description" content="System status and uptime" />
</svelte:head>

<main class="mx-auto max-w-3xl px-4 py-10 flex flex-col gap-6">
  <!-- Page title -->
  <div class="flex flex-col gap-1 px-1">
    <h1 class="text-2xl sm:text-3xl font-bold">Service Status</h1>
    <p class="text-sm text-muted-foreground">Real-time status of our services</p>
  </div>

  <!-- Overall status banner -->
  <StatusHeader status={data.overallStatus} text={data.overallStatusText} />

  <!-- Active incidents -->
  {#if data.incidents.length > 0}
    <section class="flex flex-col gap-3">
      <h2 class="text-sm font-semibold text-muted-foreground uppercase tracking-wide">Active Incidents</h2>
      {#each data.incidents as incident (incident.id)}
        <IncidentCard {incident} />
      {/each}
    </section>
  {/if}

  <!-- Ongoing maintenances -->
  {#if data.ongoingMaintenances.length > 0}
    <section class="flex flex-col gap-3">
      <h2 class="text-sm font-semibold text-muted-foreground uppercase tracking-wide">Ongoing Maintenance</h2>
      {#each data.ongoingMaintenances as m (m.id)}
        <MaintenanceCard maintenance={m} />
      {/each}
    </section>
  {/if}

  <!-- Upcoming maintenances -->
  {#if data.upcomingMaintenances.length > 0}
    <section class="flex flex-col gap-3">
      <h2 class="text-sm font-semibold text-muted-foreground uppercase tracking-wide">Upcoming Maintenance</h2>
      {#each data.upcomingMaintenances as m (m.id)}
        <MaintenanceCard maintenance={m} upcoming />
      {/each}
    </section>
  {/if}

  <!-- Services -->
  <section class="rounded-3xl border overflow-hidden">
    {#if data.services.length === 0}
      <div class="p-8 text-center text-muted-foreground text-sm">No services configured yet.</div>
    {:else}
      {#each data.services as service, i (service.slug)}
        <div class={i < data.services.length - 1 ? "border-b" : ""}>
          <ServiceRow {service} overview={data.overviewBySlug[service.slug] ?? null} />
        </div>
      {/each}
    {/if}
  </section>
</main>
