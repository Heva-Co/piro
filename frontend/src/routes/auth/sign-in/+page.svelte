<script lang="ts">
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Field from "$lib/components/ui/field/index.js";
  import * as InputGroup from "$lib/components/ui/input-group/index.js";
  import MailIcon from "@lucide/svelte/icons/mail";
  import LockIcon from "@lucide/svelte/icons/lock";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";
  import EyeClosedIcon from "@lucide/svelte/icons/eye-closed";
  import EyeIcon from "@lucide/svelte/icons/eye";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import CheckCircleIcon from "@lucide/svelte/icons/check-circle";

  import type { ActionData, PageData } from "./$types";

  let { form, data }: { form: ActionData; data: PageData } = $props();
  let loading = $state(false);
  let showPassword = $state(false);
</script>

<svelte:head><title>Sign In | Piro</title></svelte:head>

<div class="flex min-h-screen items-center justify-center p-4">
  <Card.Root class="kener-card w-full max-w-md">
    <Card.Header>
      <Card.Title>Sign In</Card.Title>
      <Card.Description>Enter your credentials to access the dashboard</Card.Description>
    </Card.Header>
    <Card.Content>
      {#if data.ssoOnly && data.oidcProviders.length > 0}
        <p class="text-sm text-muted-foreground text-center py-2">
          Password sign-in is disabled for this instance. Use SSO below.
        </p>
      {:else}
      <form method="POST" onsubmit={() => (loading = true)}>
        {#if data.invited}
          <Alert.Root class="mb-4 border-green-200 bg-green-50 text-green-800 dark:bg-green-950 dark:border-green-800 dark:text-green-300">
            <CheckCircleIcon />
            <Alert.Description>Account created! Sign in with your new credentials.</Alert.Description>
          </Alert.Root>
        {/if}

        {#if data.oidcError}
          <Alert.Root variant="destructive" class="mb-4">
            <AlertCircleIcon />
            <Alert.Description>SSO sign-in failed. Please try again or use email and password.</Alert.Description>
          </Alert.Root>
        {/if}

        {#if form?.error}
          <Alert.Root variant="destructive" class="mb-4">
            <AlertCircleIcon />
            <Alert.Title>Login failed</Alert.Title>
            <Alert.Description>{form.error}</Alert.Description>
          </Alert.Root>
        {/if}

        <Field.Group>
          <Field.Field class="flex flex-col gap-1">
            <Field.Label for="email">Email</Field.Label>
            <InputGroup.Root>
              <InputGroup.Addon><MailIcon /></InputGroup.Addon>
              <InputGroup.Input id="email" name="email" type="email" placeholder="you@example.com"
                value={form?.email ?? ""} required autocomplete="email" />
            </InputGroup.Root>
          </Field.Field>

          <Field.Field class="flex flex-col gap-1">
            <Field.Label for="password">Password</Field.Label>
            <InputGroup.Root>
              <InputGroup.Addon><LockIcon /></InputGroup.Addon>
              <InputGroup.Input id="password" name="password"
                type={showPassword ? "text" : "password"}
                placeholder="••••••••" required autocomplete="current-password" />
              <InputGroup.Addon align="inline-end">
                <InputGroup.Button type="button" size="icon-xs"
                  onclick={() => (showPassword = !showPassword)}>
                  {#if showPassword}<EyeClosedIcon class="size-4" />{:else}<EyeIcon class="size-4" />{/if}
                </InputGroup.Button>
              </InputGroup.Addon>
            </InputGroup.Root>
          </Field.Field>
        </Field.Group>

        <div class="mt-6">
          <Button type="submit" class="w-full" disabled={loading}>
            {loading ? "Signing in…" : "Sign In"}
          </Button>
        </div>
      </form>
      {/if}

      {#if data.oidcProviders.length > 0}
        <div class="relative my-6">
          <div class="absolute inset-0 flex items-center">
            <span class="w-full border-t"></span>
          </div>
          <div class="relative flex justify-center text-xs uppercase">
            <span class="bg-card px-2 text-muted-foreground">or continue with</span>
          </div>
        </div>
        <div class="flex flex-col gap-2">
          {#each data.oidcProviders as provider (provider.id)}
            <a
              href="/api/v1/auth/oidc/start?provider={provider.id}"
              class="inline-flex items-center justify-center gap-2 rounded-md border border-input bg-background px-4 py-2 text-sm font-medium shadow-sm hover:bg-accent hover:text-accent-foreground transition-colors"
            >
              {provider.displayName}
            </a>
          {/each}
        </div>
      {/if}
    </Card.Content>
  </Card.Root>
</div>
