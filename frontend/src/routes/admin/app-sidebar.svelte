<script lang="ts">
  import NavUser from "./nav-user.svelte";
  import * as Sidebar from "$lib/components/ui/sidebar/index.js";
  import { page } from "$app/state";
  import { slide } from "svelte/transition";
  import SettingsIcon from "@lucide/svelte/icons/settings";
  import { PIRO_API } from "$lib/api.js";
  import type { Component, ComponentProps } from "svelte";

  type NavItem = { title: string; url: string; icon: Component };
  let { navItems, configNavItems = [], ...restProps }: { navItems: NavItem[]; configNavItems?: NavItem[] } & ComponentProps<typeof Sidebar.Root> = $props();

  let configOpen = $state(configNavItems.some(i => page.url.pathname.startsWith(i.url)));
</script>

<Sidebar.Root collapsible="offcanvas" {...restProps}>
  <Sidebar.Header>
    <Sidebar.Menu>
      <Sidebar.MenuItem>
        <Sidebar.MenuButton class="data-[slot=sidebar-menu-button]:p-1.5!">
          {#snippet child({ props })}
            <a href="/admin" {...props} class="justify-start-safe flex items-center gap-2">
              {#if page.data.siteConfig?.logoUrl}
                <img src="{PIRO_API}{page.data.siteConfig.logoUrl}" alt="Logo" class="size-5 rounded-sm object-contain" />
              {:else}
                <img src="/piro.svg" alt="Logo" class="size-5 rounded-sm object-contain" />
              {/if}
              <span class="text-base font-semibold">{page.data.siteConfig?.name ?? 'Piro'}</span>
            </a>
          {/snippet}
        </Sidebar.MenuButton>
      </Sidebar.MenuItem>
    </Sidebar.Menu>
  </Sidebar.Header>

  <Sidebar.Content>
    <Sidebar.Group>
      <Sidebar.GroupContent class="flex flex-col gap-2">
        <Sidebar.Menu>
          <!-- Main nav items -->
          {#each navItems as item (item.title)}
            <Sidebar.MenuItem>
              <Sidebar.MenuButton tooltipContent={item.title} isActive={item.url === "/admin" ? page.url.pathname === "/admin" : page.url.pathname.startsWith(item.url)}>
                {#snippet child({ props })}
                  <a href={item.url} {...props}>
                    <item.icon />
                    <span>{item.title}</span>
                  </a>
                {/snippet}
              </Sidebar.MenuButton>
            </Sidebar.MenuItem>
          {/each}

          <!-- Configuration collapsible -->
          {#if configNavItems.length > 0}
            <Sidebar.MenuItem>
              <Sidebar.MenuButton tooltipContent="Configuration" isActive={false}>
                {#snippet child({ props })}
                  <button {...props} onclick={() => configOpen = !configOpen}>
                    <SettingsIcon class="size-4 shrink-0" />
                    <span>Configuration</span>
                    <svg class="ml-auto size-3.5 transition-transform duration-200 {configOpen ? 'rotate-180' : ''}" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6"/></svg>
                  </button>
                {/snippet}
              </Sidebar.MenuButton>

              {#if configOpen}
                <div transition:slide={{ duration: 200 }}>
                  <Sidebar.MenuSub class="mt-1 gap-2">
                    {#each configNavItems as item (item.title)}
                      <Sidebar.MenuSubItem>
                        <Sidebar.MenuSubButton isActive={page.url.pathname.startsWith(item.url)} class="min-h-8">
                          {#snippet child({ props })}
                            <a href={item.url} {...props}>
                              <item.icon class="size-4" />
                              <span>{item.title}</span>
                            </a>
                          {/snippet}
                        </Sidebar.MenuSubButton>
                      </Sidebar.MenuSubItem>
                    {/each}
                  </Sidebar.MenuSub>
                </div>
              {/if}
            </Sidebar.MenuItem>
          {/if}
        </Sidebar.Menu>
      </Sidebar.GroupContent>
    </Sidebar.Group>
  </Sidebar.Content>

  <Sidebar.Footer>
    <NavUser />
  </Sidebar.Footer>
</Sidebar.Root>
