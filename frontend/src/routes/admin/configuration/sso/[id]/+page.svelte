<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/state";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import * as Select from "$lib/components/ui/select/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Switch } from "$lib/components/ui/switch/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { toast } from "svelte-sonner";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import CheckCircleIcon from "@lucide/svelte/icons/check-circle";
  import CopyIcon from "@lucide/svelte/icons/copy";
  import ArrowLeftIcon from "@lucide/svelte/icons/arrow-left";
  import type { RoleDto } from "$lib/api.js";

  // Effective redirect URI: always points to the frontend's public /auth/oidc/callback route.
  // page.url.origin gives the correct public origin in both dev and production.
  const effectiveRedirectUri = $derived(`${page.url.origin}/auth/oidc/callback`);

  const providerId = $derived(page.params.id);
  const isNew = $derived(providerId === "new");

  let loading = $state(true);
  let saving = $state(false);
  let testing = $state(false);
  let testResult = $state<{ success: boolean; message: string } | null>(null);
  let roles = $state<RoleDto[]>([]);

  let form = $state({
    id: "",
    displayName: "",
    authority: "",
    clientId: "",
    clientSecret: "",
    scopes: "openid, profile, email",
    allowedDomains: "",
    defaultRole: "Viewer",
    isEnabled: true,
  });

  async function api(action: string, data?: unknown) {
    const res = await fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action, data }),
    });
    return res.json();
  }

  async function load() {
    loading = true;
    try {
      const rolesRes = await api("getRoles");
      if (!rolesRes.error) roles = rolesRes;

      if (!isNew) {
        const configs: { id: string; displayName: string; authority: string; clientId: string; redirectUri: string; scopes: string; allowedDomains: string | null; defaultRole: string; isEnabled: boolean }[] = await api("getOidcConfigs");
        const found = Array.isArray(configs) ? configs.find((c) => c.id === providerId) : null;
        if (found) {
          form = {
            id: found.id,
            displayName: found.displayName,
            authority: found.authority,
            clientId: found.clientId,
            clientSecret: "",
            scopes: found.scopes,
            allowedDomains: found.allowedDomains ?? "",
            defaultRole: found.defaultRole,
            isEnabled: found.isEnabled,
          };
        }
      }
    } finally {
      loading = false;
    }
  }

  async function save() {
    if (!form.id.trim() || !form.displayName.trim() || !form.authority.trim() || !form.clientId.trim()) return;
    saving = true;
    try {
      const result = await api("upsertOidcConfig", {
        id: form.id.trim(),
        displayName: form.displayName.trim(),
        authority: form.authority.trim(),
        clientId: form.clientId.trim(),
        clientSecret: form.clientSecret.trim() || null,
        redirectUri: null, // always auto-derived from site URL on the backend
        scopes: form.scopes.trim() || "openid, profile, email",
        allowedDomains: form.allowedDomains.trim() || null,
        defaultRole: form.defaultRole,
        isEnabled: form.isEnabled,
      });
      if (result.error) { toast.error(result.error); return; }
      toast.success(`Provider "${form.displayName}" saved.`);
      goto("/admin/configuration/sso");
    } catch { toast.error("Failed to save provider."); }
    finally { saving = false; }
  }

  async function testProvider() {
    const id = isNew ? form.id.trim() : providerId;
    if (!id) return;
    testing = true;
    testResult = null;
    try {
      const result = await api("testOidcProvider", { providerId: id });
      if (result.error) {
        testResult = { success: false, message: result.error };
      } else {
        testResult = { success: true, message: result.message ?? "Discovery document fetched successfully." };
      }
    } catch { testResult = { success: false, message: "Test failed." }; }
    finally { testing = false; }
  }

  async function copyRedirectUri() {
    try {
      await navigator.clipboard.writeText(effectiveRedirectUri);
      toast.success("Redirect URI copied.");
    } catch { toast.error("Failed to copy."); }
  }

  $effect(() => { load(); });
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div class="flex items-center gap-3">
    <Button variant="ghost" size="icon" class="size-8" onclick={() => goto("/admin/configuration/sso")}>
      <ArrowLeftIcon class="size-4" />
    </Button>
    <div>
      <h1 class="text-xl font-semibold">{isNew ? "Add OIDC Provider" : "Edit OIDC Provider"}</h1>
      <p class="text-sm text-muted-foreground">
        Works with any standard OIDC/OAuth2 provider.
        <a
          href="https://github.com/Heva-Co/piro/wiki/Configuring-SSO"
          target="_blank"
          rel="noopener noreferrer"
          class="text-primary underline underline-offset-4 hover:text-primary/80"
        >Provider setup guides →</a>
      </p>
    </div>
  </div>

  {#if loading}
    <div class="flex justify-center py-12">
      <Spinner class="size-5 text-muted-foreground" />
    </div>
  {:else}
    <Card.Root>
      <Card.Content class="space-y-5 pt-6">

        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-2">
            <Label for="oidc-id">Provider ID</Label>
            <Input
              id="oidc-id"
              placeholder="google"
              bind:value={form.id}
              disabled={!isNew}
            />
            <p class="text-xs text-muted-foreground">Lowercase slug, e.g. "google"</p>
          </div>
          <div class="space-y-2">
            <Label for="oidc-display">Display Name</Label>
            <Input id="oidc-display" placeholder="Google" bind:value={form.displayName} />
          </div>
        </div>

        <div class="space-y-2">
          <Label for="oidc-authority">Authority URL</Label>
          <Input id="oidc-authority" placeholder="https://accounts.google.com" bind:value={form.authority} />
          <p class="text-xs text-muted-foreground">
            Discovery document: <code class="text-xs">{form.authority || "https://..."}</code>/.well-known/openid-configuration
          </p>
        </div>

        <div class="space-y-2">
          <Label for="oidc-client-id">Client ID</Label>
          <Input id="oidc-client-id" bind:value={form.clientId} />
        </div>

        <div class="space-y-2">
          <Label for="oidc-client-secret">Client Secret</Label>
          <Input
            id="oidc-client-secret"
            type="password"
            placeholder={!isNew ? "Leave blank to keep existing" : ""}
            bind:value={form.clientSecret}
          />
        </div>

        <div class="space-y-2">
          <Label for="oidc-redirect">Redirect URI</Label>
          <div class="flex gap-2">
            <Input id="oidc-redirect" value={effectiveRedirectUri} disabled class="font-mono text-xs" />
            <Button variant="outline" size="icon" onclick={copyRedirectUri} title="Copy to clipboard">
              <CopyIcon class="size-4" />
            </Button>
          </div>
          <p class="text-xs text-muted-foreground">Register this in your provider's allowed redirect URIs.</p>
        </div>

        <div class="space-y-2">
          <Label for="oidc-scopes">Scopes</Label>
          <Input id="oidc-scopes" placeholder="openid, profile, email" bind:value={form.scopes} />
          <p class="text-xs text-muted-foreground">Comma-separated list of OAuth2 scopes.</p>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-2">
            <Label for="oidc-domains">Allowed Email Domains</Label>
            <Input id="oidc-domains" placeholder="example.com, another.org" bind:value={form.allowedDomains} />
            <p class="text-xs text-muted-foreground">Comma-separated. Blank = allow all.</p>
          </div>
          <div class="space-y-2">
            <Label for="oidc-role">Default Role</Label>
            <Select.Root
              type="single"
              value={form.defaultRole}
              onValueChange={(v) => { if (v) form.defaultRole = v; }}
            >
              <Select.Trigger id="oidc-role" class="w-full">
                {form.defaultRole || "Select a role"}
              </Select.Trigger>
              <Select.Content>
                {#each roles.filter((r) => r.name !== "Owner") as role (role.id)}
                  <Select.Item value={role.name}>{role.name}</Select.Item>
                {/each}
              </Select.Content>
            </Select.Root>
            <p class="text-xs text-muted-foreground">Assigned to new users on first sign-in.</p>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <Switch id="oidc-enabled" bind:checked={form.isEnabled} />
          <Label for="oidc-enabled">Enabled</Label>
        </div>

        {#if testResult}
          <Alert.Root variant={testResult.success ? "default" : "destructive"}>
            {#if testResult.success}
              <CheckCircleIcon />
            {:else}
              <AlertCircleIcon />
            {/if}
            <Alert.Description>{testResult.message}</Alert.Description>
          </Alert.Root>
        {/if}

      </Card.Content>
    </Card.Root>

    <div class="flex items-center gap-3">
      <Button variant="outline" onclick={testProvider} disabled={testing || isNew} title={isNew ? "Save the provider first before testing" : undefined}>
        {#if testing}<Spinner class="size-4 mr-1" />{/if}
        Test Connection
      </Button>
      <div class="ml-auto flex gap-2">
        <Button variant="outline" onclick={() => goto("/admin/configuration/sso")}>Cancel</Button>
        <Button
          onclick={save}
          disabled={saving || !form.id.trim() || !form.displayName.trim() || !form.authority.trim() || !form.clientId.trim()}
        >
          {#if saving}<Spinner class="size-4 mr-1" />{/if}
          Save Provider
        </Button>
      </div>
    </div>
  {/if}
</div>
