<script lang="ts">
  import DotsVerticalIcon from "@lucide/svelte/icons/ellipsis-vertical";
  import LogoutIcon from "@lucide/svelte/icons/log-out";
  import UserCircleIcon from "@lucide/svelte/icons/user-circle";
  import CheckIcon from "@lucide/svelte/icons/check";
  import LoaderIcon from "@lucide/svelte/icons/loader";
  import Sun from "@lucide/svelte/icons/sun";
  import Moon from "@lucide/svelte/icons/moon";

  import * as Avatar from "$lib/components/ui/avatar/index.js";
  import * as DropdownMenu from "$lib/components/ui/dropdown-menu/index.js";
  import * as Sidebar from "$lib/components/ui/sidebar/index.js";
  import * as Dialog from "$lib/components/ui/dialog/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { page } from "$app/state";
  import { toggleMode, mode } from "mode-watcher";
  import type { UserDto } from "$lib/api";

  const user = $derived(page.data.user as UserDto | undefined);
  const nameAbbr = $derived(
    (user?.name || user?.email || "?")
      .split(" ")
      .map((n: string) => n[0])
      .join("")
      .slice(0, 2)
      .toUpperCase()
  );

  const sidebar = Sidebar.useSidebar();

  let accountDialogOpen = $state(false);
  let myName = $state("");
  let myPassword = $state("");
  let confirmPassword = $state("");
  let savingName = $state(false);
  let resettingPass = $state(false);
  let nameError = $state("");
  let passwordError = $state("");
  let nameSuccess = $state(false);
  let passwordSuccess = $state(false);

  let hasMinLength = $derived(myPassword.length >= 8);
  let passwordsMatch = $derived(myPassword === confirmPassword && myPassword !== "");
  let isPasswordValid = $derived(hasMinLength && passwordsMatch);

  function openAccountDialog() {
    myName = user?.name ?? "";
    myPassword = "";
    confirmPassword = "";
    nameError = "";
    passwordError = "";
    nameSuccess = false;
    passwordSuccess = false;
    accountDialogOpen = true;
  }

  async function saveName() {
    savingName = true;
    nameError = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "updateUser", data: { name: myName } }),
      });
      const result = await res.json();
      if (result.error) nameError = result.error;
      else { nameSuccess = true; setTimeout(() => (nameSuccess = false), 2000); }
    } catch { nameError = "Failed to save name."; }
    finally { savingName = false; }
  }

  async function updatePassword() {
    resettingPass = true;
    passwordError = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "updatePassword", data: { newPassword: myPassword } }),
      });
      const result = await res.json();
      if (result.error) passwordError = result.error;
      else {
        myPassword = "";
        confirmPassword = "";
        passwordSuccess = true;
        setTimeout(() => (passwordSuccess = false), 2000);
      }
    } catch { passwordError = "Failed to update password."; }
    finally { resettingPass = false; }
  }
</script>

<Sidebar.Menu>
  <Sidebar.MenuItem>
    <DropdownMenu.Root>
      <DropdownMenu.Trigger>
        {#snippet child({ props })}
          <Sidebar.MenuButton
            {...props}
            size="lg"
            class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
          >
            <Avatar.Root class="size-8 rounded-lg grayscale">
              <Avatar.Fallback class="rounded-lg">{nameAbbr}</Avatar.Fallback>
            </Avatar.Root>
            <div class="grid flex-1 text-start text-sm leading-tight">
              <span class="truncate font-medium">{user?.name || "User"}</span>
              <span class="text-muted-foreground truncate text-xs">{user?.email}</span>
            </div>
            <DotsVerticalIcon class="ms-auto size-4" />
          </Sidebar.MenuButton>
        {/snippet}
      </DropdownMenu.Trigger>
      <DropdownMenu.Content
        class="w-(--bits-dropdown-menu-anchor-width) min-w-56 rounded-lg"
        side={sidebar.isMobile ? "bottom" : "right"}
        align="end"
        sideOffset={4}
      >
        <DropdownMenu.Label class="p-0 font-normal">
          <div class="flex items-center gap-2 px-1 py-1.5 text-start text-sm">
            <Avatar.Root class="size-8 rounded-lg">
              <Avatar.Fallback class="rounded-lg">{nameAbbr}</Avatar.Fallback>
            </Avatar.Root>
            <div class="grid flex-1 text-start text-sm leading-tight">
              <span class="truncate font-medium">{user?.name || "User"}</span>
              <span class="text-muted-foreground truncate text-xs">{user?.email}</span>
            </div>
          </div>
        </DropdownMenu.Label>
        <DropdownMenu.Separator />
        <DropdownMenu.Group>
          <DropdownMenu.Item onclick={openAccountDialog}>
            <UserCircleIcon />
            Account
          </DropdownMenu.Item>
          <DropdownMenu.Item onclick={toggleMode}>
            <Sun class="absolute scale-100 rotate-0 transition-all dark:scale-0 dark:-rotate-90" />
            <Moon class="absolute scale-0 rotate-90 transition-all dark:scale-100 dark:rotate-0" />
            <span class="pl-6">{mode.current === "light" ? "Light" : "Dark"}</span>
          </DropdownMenu.Item>
        </DropdownMenu.Group>
        <DropdownMenu.Separator />
        <DropdownMenu.Item>
          {#snippet child({ props })}
            <form method="POST" action="/auth/sign-out" class="w-full">
              <Button {...props} type="submit" variant="ghost" class="w-full justify-start">
                <LogoutIcon />
                Sign out
              </Button>
            </form>
          {/snippet}
        </DropdownMenu.Item>
      </DropdownMenu.Content>
    </DropdownMenu.Root>
  </Sidebar.MenuItem>
</Sidebar.Menu>

<Dialog.Root bind:open={accountDialogOpen}>
  <Dialog.Content class="max-w-md">
    <Dialog.Header>
      <Dialog.Title>Account Settings</Dialog.Title>
      <Dialog.Description>
        {user?.email}
      </Dialog.Description>
    </Dialog.Header>
    <div class="flex flex-col gap-6 py-4">
      <form class="flex flex-col gap-3" onsubmit={(e) => { e.preventDefault(); saveName(); }}>
        <Label for="account-name">Name</Label>
        <div class="flex gap-2">
          <Input id="account-name" bind:value={myName} placeholder="Your name" disabled={savingName} class="flex-1" />
          <Button type="submit" disabled={savingName || !myName.trim()}>
            {#if savingName}<LoaderIcon class="size-4 animate-spin" />
            {:else if nameSuccess}<CheckIcon class="size-4" />
            {:else}Save{/if}
          </Button>
        </div>
        {#if nameError}<p class="text-destructive text-sm">{nameError}</p>{/if}
      </form>
      <hr />
      <form class="flex flex-col gap-3" onsubmit={(e) => { e.preventDefault(); updatePassword(); }}>
        <Label>Change Password</Label>
        <Input type="password" bind:value={myPassword} placeholder="New password (min 8 chars)" disabled={resettingPass} />
        <Input type="password" bind:value={confirmPassword} placeholder="Confirm password" disabled={resettingPass} />
        <Button type="submit" disabled={resettingPass || !isPasswordValid}>
          {#if resettingPass}<LoaderIcon class="size-4 animate-spin" /> Updating...
          {:else if passwordSuccess}<CheckIcon class="size-4" /> Updated!
          {:else}Update Password{/if}
        </Button>
        {#if passwordError}<p class="text-destructive text-sm">{passwordError}</p>{/if}
      </form>
    </div>
  </Dialog.Content>
</Dialog.Root>
