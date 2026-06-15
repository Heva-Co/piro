<script lang="ts">
  import { goto } from "$app/navigation";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import * as Tabs from "$lib/components/ui/tabs/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Badge } from "$lib/components/ui/badge/index.js";
  import { Switch } from "$lib/components/ui/switch/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { toast } from "svelte-sonner";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import PencilIcon from "@lucide/svelte/icons/pencil";
  import type { OidcProviderConfigDto } from "$lib/api.js";

  let providers = $state<OidcProviderConfigDto[]>([]);
  let loading = $state(true);
  let error = $state("");
  let ssoOnly = $state(false);
  let savingSsoMode = $state(false);

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
      const [providersRes, modeRes] = await Promise.all([
        api("getOidcConfigs"),
        api("getSsoMode"),
      ]);
      if (providersRes.error) { error = providersRes.error; return; }
      providers = providersRes;
      ssoOnly = modeRes.ssoOnly ?? false;
    } catch { error = "Failed to load SSO configuration."; }
    finally { loading = false; }
  }

  async function toggleSsoOnly(value: boolean) {
    savingSsoMode = true;
    try {
      const result = await api("setSsoMode", { ssoOnly: value });
      if (result.error) { toast.error(result.error); return; }
      ssoOnly = value;
      toast.success(value ? "SSO-only mode enabled. Password sign-in is now disabled." : "Password sign-in re-enabled.");
    } catch { toast.error("Failed to update SSO mode."); }
    finally { savingSsoMode = false; }
  }

  $effect(() => { load(); });
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div class="space-y-1">
    <h1 class="text-xl font-semibold">Single Sign-On</h1>
    <p class="text-sm text-muted-foreground">
      Configure identity providers so your team can sign in with their existing accounts.
      <a
        href="https://github.com/Heva-Co/piro/wiki/Configuring-SSO"
        target="_blank"
        rel="noopener noreferrer"
        class="text-primary underline underline-offset-4 hover:text-primary/80"
      >Learn how to set up SSO →</a>
    </p>
  </div>

  {#if error}
    <Alert.Root variant="destructive">
      <AlertCircleIcon />
      <Alert.Description>{error}</Alert.Description>
    </Alert.Root>
  {/if}

  <!-- SSO-only mode toggle -->
  <Card.Root>
    <Card.Content class="pt-5">
      <div class="flex items-start justify-between gap-4">
        <div class="space-y-1">
          <p class="text-sm font-medium">SSO-only mode</p>
          <p class="text-sm text-muted-foreground">
            Disables password-based sign-in for all users. Only SSO providers configured below will work.
            {#if ssoOnly}
              <span class="text-destructive font-medium"> Make sure at least one provider is enabled before activating this.</span>
            {/if}
          </p>
        </div>
        <div class="flex items-center gap-2 shrink-0">
          {#if savingSsoMode}<Spinner class="size-4 text-muted-foreground" />{/if}
          <Switch
            id="sso-only"
            checked={ssoOnly}
            disabled={savingSsoMode || loading}
            onCheckedChange={toggleSsoOnly}
          />
          <Label for="sso-only" class="text-sm">{ssoOnly ? "Enabled" : "Disabled"}</Label>
        </div>
      </div>
    </Card.Content>
  </Card.Root>

  <Tabs.Root value="oidc">
    <Tabs.List>
      <Tabs.Trigger value="oidc">OIDC / OAuth2</Tabs.Trigger>
      <Tabs.Trigger value="saml">SAML 2.0</Tabs.Trigger>
    </Tabs.List>

    <Tabs.Content value="oidc" class="mt-4">
      <div class="flex items-center justify-between mb-3">
        <p class="text-sm text-muted-foreground">OpenID Connect providers (Google, Microsoft, Okta, …)</p>
        <Button onclick={() => goto("/admin/configuration/sso/new")}>
          <PlusIcon class="size-4 mr-1" /> Add Provider
        </Button>
      </div>

      <Card.Root>
        {#if loading}
          <Card.Content class="flex justify-center py-12">
            <Spinner class="size-5 text-muted-foreground" />
          </Card.Content>
        {:else if providers.length === 0}
          <Card.Content class="py-12 text-center text-sm text-muted-foreground">
            No OIDC providers configured yet.
          </Card.Content>
        {:else}
          <div class="divide-y">
            {#each providers as provider (provider.id)}
              <div class="flex items-center gap-4 px-6 py-4">
                <div class="flex-1 min-w-0">
                  <p class="text-sm font-medium">{provider.displayName}</p>
                  <p class="text-xs text-muted-foreground truncate">{provider.authority}</p>
                </div>
                <div class="flex items-center gap-3 shrink-0">
                  <Badge variant={provider.isEnabled ? "default" : "secondary"}>
                    {provider.isEnabled ? "Enabled" : "Disabled"}
                  </Badge>
                  <Badge variant="outline">{provider.defaultRole}</Badge>
                  <Button
                    variant="ghost"
                    size="icon"
                    class="size-8"
                    onclick={() => goto(`/admin/configuration/sso/${provider.id}`)}
                  >
                    <PencilIcon class="size-4" />
                  </Button>
                </div>
              </div>
            {/each}
          </div>
        {/if}
      </Card.Root>
    </Tabs.Content>

    <Tabs.Content value="saml" class="mt-4">
      <Card.Root>
        <Card.Content class="py-12 text-center text-sm text-muted-foreground">
          SAML 2.0 support is coming soon.
        </Card.Content>
      </Card.Root>
    </Tabs.Content>
  </Tabs.Root>
</div>
