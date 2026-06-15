<script lang="ts">
  import { ModeWatcher } from "mode-watcher";
  import { page } from "$app/state";
  import * as Sidebar from "$lib/components/ui/sidebar/index.js";
  import * as Tooltip from "$lib/components/ui/tooltip/index.js";
  import { Toaster } from "$lib/components/ui/sonner/index.js";
  import AppSidebar from "./app-sidebar.svelte";
  import SiteHeader from "./site-header.svelte";

  import BlendIcon from "@lucide/svelte/icons/blend";
  import BellIcon from "@lucide/svelte/icons/bell";
  import CloudAlertIcon from "@lucide/svelte/icons/cloud-alert";
  import ClockAlertIcon from "@lucide/svelte/icons/clock-alert";
  import LayoutDashboardIcon from "@lucide/svelte/icons/layout-dashboard";
  import UploadIcon from "@lucide/svelte/icons/upload";
  import KeyIcon from "@lucide/svelte/icons/key";
  import ActivityIcon from "@lucide/svelte/icons/activity";
  import ServerIcon from "@lucide/svelte/icons/server";
  import ScrollTextIcon from "@lucide/svelte/icons/scroll-text";
  import UsersIcon from "@lucide/svelte/icons/users";
  import KeyRoundIcon from "@lucide/svelte/icons/key-round";
  import GlobeIcon from "@lucide/svelte/icons/globe";

  let { children, data } = $props();

  const navItems = [
    { title: "Overview",      url: "/admin",              icon: LayoutDashboardIcon },
    { title: "Services",      url: "/admin/services",     icon: BlendIcon },
    { title: "Checks",        url: "/admin/checks",       icon: ActivityIcon },
    { title: "Incidents",     url: "/admin/incidents",    icon: CloudAlertIcon },
    { title: "Maintenances",  url: "/admin/maintenances", icon: ClockAlertIcon },
    { title: "Notification Channels", url: "/admin/triggers", icon: BellIcon },
    { title: "Logs",          url: "/admin/logs",         icon: ScrollTextIcon },
  ];

  const configNavItems = [
    { title: "Import",   url: "/admin/configuration/import",    icon: UploadIcon },
    { title: "API Keys", url: "/admin/configuration/api-keys",  icon: KeyIcon },
    { title: "Workers",  url: "/admin/configuration/workers",   icon: ServerIcon },
    { title: "Users",    url: "/admin/configuration/users",     icon: UsersIcon },
    { title: "SSO",       url: "/admin/configuration/sso",       icon: KeyRoundIcon },
    { title: "Site",      url: "/admin/configuration/site",      icon: GlobeIcon },
  ];

  const allItems = [...navItems, ...configNavItems];

  let pageTitle = $derived(
    allItems
      .filter((item) => page.url.pathname.startsWith(item.url))
      .sort((a, b) => b.url.length - a.url.length)[0]?.title ?? "Admin"
  );
</script>

<ModeWatcher />
<Toaster richColors position="top-right" />

<svelte:head>
  <meta name="robots" content="noindex, nofollow" />
  <title>{pageTitle} | {page.data.siteConfig?.name ?? 'Piro'}</title>
</svelte:head>

<Sidebar.Provider style="--sidebar-width: calc(var(--spacing) * 64); --header-height: calc(var(--spacing) * 12);">
  <AppSidebar variant="inset" {navItems} {configNavItems} />
  <Sidebar.Inset>
    <SiteHeader title={pageTitle} />
    <div class="p-4">
      <div class="@container/main flex flex-1">
        <Tooltip.Provider>
          {@render children()}
        </Tooltip.Provider>
      </div>
    </div>
  </Sidebar.Inset>
</Sidebar.Provider>
