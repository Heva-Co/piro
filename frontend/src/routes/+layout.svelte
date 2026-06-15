<script lang="ts">
  import "../app.css";
  import { ModeWatcher } from "mode-watcher";
  import { Toaster } from "svelte-sonner";
  import { page } from "$app/state";
  import Nav from "$lib/components/Nav.svelte";
  import { PIRO_API } from "$lib/api.js";

  let { children, data } = $props();

  const showNav = $derived(
    !page.url.pathname.startsWith("/admin") &&
    !page.url.pathname.startsWith("/auth")
  );

  const cfg = $derived(data.siteConfig);
</script>

<svelte:head>
  {#if cfg?.faviconUrl}
    <link rel="icon" type="image/png" href="{PIRO_API}{cfg.faviconUrl}" />
  {/if}
  {#if cfg?.metaDescription}
    <meta name="description" content={cfg.metaDescription} />
    <meta property="og:description" content={cfg.metaDescription} />
  {/if}
  {#if cfg?.ogImageUrl}
    <meta property="og:image" content="{PIRO_API}{cfg.ogImageUrl}" />
  {/if}
</svelte:head>

<ModeWatcher />
<Toaster richColors position="top-right" />

{#if showNav}
  <Nav user={data.user} />
{/if}

{@render children()}
