<script lang="ts">
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import * as Dialog from "$lib/components/ui/dialog/index.js";
  import * as AlertDialog from "$lib/components/ui/alert-dialog/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { toast } from "svelte-sonner";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import TrashIcon from "@lucide/svelte/icons/trash-2";
  import UserIcon from "@lucide/svelte/icons/user";
  import type { UserListDto, RoleDto } from "$lib/api.js";

  let users = $state<UserListDto[]>([]);
  let roles = $state<RoleDto[]>([]);
  let loading = $state(true);
  let error = $state("");

  // Invite dialog
  let inviteOpen = $state(false);
  let inviteEmail = $state("");
  let inviteRoleId = $state<number | null>(null);
  let inviting = $state(false);

  // Change role dialog
  let changeRoleTarget = $state<UserListDto | null>(null);
  let changeRoleId = $state<number | null>(null);
  let changingRole = $state(false);

  // Delete confirmation
  let deleteTarget = $state<UserListDto | null>(null);
  let deleting = $state(false);

  async function api(action: string, data?: unknown) {
    const res = await fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action, data }),
    });
    return res.json();
  }

  async function load() {
    loading = true; error = "";
    try {
      const [usersRes, rolesRes] = await Promise.all([
        api("getUsers"),
        api("getRoles"),
      ]);
      if (usersRes.error) { error = usersRes.error; return; }
      if (rolesRes.error) { error = rolesRes.error; return; }
      users = usersRes;
      roles = rolesRes;
    } catch { error = "Failed to load users."; }
    finally { loading = false; }
  }

  async function invite() {
    if (!inviteEmail.trim() || !inviteRoleId) return;
    inviting = true;
    try {
      const result = await api("inviteUser", { email: inviteEmail.trim(), roleId: inviteRoleId });
      if (result.error) { toast.error(result.error); return; }
      toast.success(`Invitation sent to ${inviteEmail.trim()}.`);
      inviteOpen = false;
      inviteEmail = "";
      inviteRoleId = null;
      await load();
    } catch { toast.error("Failed to send invitation."); }
    finally { inviting = false; }
  }

  async function changeRole() {
    if (!changeRoleTarget || !changeRoleId) return;
    changingRole = true;
    try {
      const result = await api("changeUserRole", { userId: changeRoleTarget.id, roleId: changeRoleId });
      if (result.error) { toast.error(result.error); return; }
      toast.success("Role updated.");
      changeRoleTarget = null;
      changeRoleId = null;
      await load();
    } catch { toast.error("Failed to change role."); }
    finally { changingRole = false; }
  }

  async function deleteUser() {
    if (!deleteTarget) return;
    deleting = true;
    try {
      const result = await api("deleteUser", { userId: deleteTarget.id });
      if (result.error) { toast.error(result.error); return; }
      toast.success(`${deleteTarget.email} removed.`);
      deleteTarget = null;
      await load();
    } catch { toast.error("Failed to remove user."); }
    finally { deleting = false; }
  }

  function openChangeRole(user: UserListDto) {
    changeRoleTarget = user;
    const currentRoleName = user.roles[0] ?? "";
    changeRoleId = roles.find((r) => r.name === currentRoleName)?.id ?? null;
  }

  $effect(() => {
    load();
  });
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div class="flex items-center justify-between">
    <div class="space-y-1">
      <h1 class="text-xl font-semibold">Users</h1>
      <p class="text-sm text-muted-foreground">Manage team members and their access roles.</p>
    </div>
    <Button onclick={() => { inviteOpen = true; inviteEmail = ""; inviteRoleId = roles[0]?.id ?? null; }}>
      <PlusIcon class="size-4 mr-1" /> Invite User
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
    {:else if users.length === 0}
      <Card.Content class="py-12 text-center text-sm text-muted-foreground">
        No users found.
      </Card.Content>
    {:else}
      <div class="divide-y">
        {#each users as user (user.id)}
          <div class="flex items-center gap-4 px-6 py-4">
            <div class="flex h-8 w-8 items-center justify-center rounded-full bg-muted shrink-0">
              <UserIcon class="size-4 text-muted-foreground" />
            </div>
            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium truncate">
                {#if user.name}
                  {user.name}
                {:else}
                  <span class="text-muted-foreground italic">Pending</span>
                {/if}
              </p>
              <p class="text-xs text-muted-foreground truncate">{user.email}</p>
            </div>
            <div class="flex items-center gap-3 shrink-0">
              {#if user.isPending}
                <Badge variant="secondary">Pending</Badge>
              {:else if !user.isActive}
                <Badge variant="destructive">Inactive</Badge>
              {/if}
              {#each user.roles as role}
                <Badge variant={role === "Owner" ? "default" : "secondary"}>{role}</Badge>
              {/each}
              <p class="text-xs text-muted-foreground hidden sm:block">
                {new Date(user.createdAt).toLocaleDateString()}
              </p>
              <Button
                variant="ghost"
                size="sm"
                class="text-muted-foreground hover:text-foreground text-xs"
                onclick={() => openChangeRole(user)}
              >
                Change role
              </Button>
              {#if !user.roles.includes("Owner")}
                <Button
                  variant="ghost"
                  size="icon"
                  class="size-8 text-muted-foreground hover:text-destructive"
                  onclick={() => deleteTarget = user}
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

<!-- Invite dialog -->
<Dialog.Root bind:open={inviteOpen}>
  <Dialog.Content class="sm:max-w-md">
    <Dialog.Header>
      <Dialog.Title>Invite User</Dialog.Title>
      <Dialog.Description>
        Send an invitation email to add a new team member.
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4">
      <div class="space-y-2">
        <Label for="invite-email">Email address</Label>
        <Input
          id="invite-email"
          type="email"
          placeholder="colleague@example.com"
          bind:value={inviteEmail}
        />
      </div>
      <div class="space-y-2">
        <Label for="invite-role">Role</Label>
        <Select.Root
          type="single"
          value={inviteRoleId?.toString() ?? ""}
          onValueChange={(v) => inviteRoleId = v ? parseInt(v) : null}
        >
          <Select.Trigger id="invite-role" class="w-full">
            {roles.find((r) => r.id === inviteRoleId)?.name ?? "Select a role"}
          </Select.Trigger>
          <Select.Content>
            {#each roles.filter((r) => r.name !== "Owner") as role (role.id)}
              <Select.Item value={role.id.toString()}>{role.name}</Select.Item>
            {/each}
          </Select.Content>
        </Select.Root>
      </div>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => inviteOpen = false}>Cancel</Button>
      <Button
        onclick={invite}
        disabled={inviting || !inviteEmail.trim() || !inviteRoleId}
      >
        {#if inviting}<Spinner class="size-4 mr-1" />{/if}
        Send Invitation
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Change role dialog -->
<Dialog.Root open={!!changeRoleTarget} onOpenChange={(open) => { if (!open) changeRoleTarget = null; }}>
  <Dialog.Content class="sm:max-w-sm">
    <Dialog.Header>
      <Dialog.Title>Change Role</Dialog.Title>
      <Dialog.Description>
        Update the role for <strong>{changeRoleTarget?.email}</strong>.
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-2">
      <Label for="change-role">Role</Label>
      <Select.Root
        type="single"
        value={changeRoleId?.toString() ?? ""}
        onValueChange={(v) => changeRoleId = v ? parseInt(v) : null}
      >
        <Select.Trigger id="change-role" class="w-full">
          {roles.find((r) => r.id === changeRoleId)?.name ?? "Select a role"}
        </Select.Trigger>
        <Select.Content>
          {#each roles.filter((r) => r.name !== "Owner") as role (role.id)}
            <Select.Item value={role.id.toString()}>{role.name}</Select.Item>
          {/each}
        </Select.Content>
      </Select.Root>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => changeRoleTarget = null}>Cancel</Button>
      <Button onclick={changeRole} disabled={changingRole || !changeRoleId}>
        {#if changingRole}<Spinner class="size-4 mr-1" />{/if}
        Save
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Delete confirmation -->
<AlertDialog.Root open={!!deleteTarget} onOpenChange={(open) => { if (!open) deleteTarget = null; }}>
  <AlertDialog.Content>
    <AlertDialog.Header>
      <AlertDialog.Title>Remove User</AlertDialog.Title>
      <AlertDialog.Description>
        Are you sure you want to remove <strong>{deleteTarget?.email}</strong>?
        They will lose all access immediately.
      </AlertDialog.Description>
    </AlertDialog.Header>
    <AlertDialog.Footer>
      <AlertDialog.Cancel onclick={() => deleteTarget = null}>Cancel</AlertDialog.Cancel>
      <AlertDialog.Action
        class="bg-destructive text-white hover:bg-destructive/90"
        onclick={deleteUser}
        disabled={deleting}
      >
        {#if deleting}<Spinner class="size-4 mr-1" />{/if}
        Remove
      </AlertDialog.Action>
    </AlertDialog.Footer>
  </AlertDialog.Content>
</AlertDialog.Root>
