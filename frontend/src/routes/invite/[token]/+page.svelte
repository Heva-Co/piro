<script lang="ts">
  import { enhance } from "$app/forms";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import * as Alert from "$lib/components/ui/alert/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import AlertCircleIcon from "@lucide/svelte/icons/alert-circle";

  let { data, form } = $props();

  let name = $state(form?.name ?? "");
  let password = $state("");
  let confirmPassword = $state("");
  let submitting = $state(false);
</script>

<svelte:head><title>Accept Invitation — Piro</title></svelte:head>

<div class="min-h-screen flex items-center justify-center bg-background p-4">
  <div class="w-full max-w-sm space-y-6">
    <div class="space-y-1 text-center">
      <h1 class="text-2xl font-bold tracking-tight">Set up your account</h1>
      <p class="text-sm text-muted-foreground">Enter your name and choose a password to complete your invitation.</p>
    </div>

    {#if form?.error}
      <Alert.Root variant="destructive">
        <AlertCircleIcon />
        <Alert.Description>{form.error}</Alert.Description>
      </Alert.Root>
    {/if}

    <form
      method="POST"
      use:enhance={() => {
        submitting = true;
        return async ({ update }) => {
          submitting = false;
          update();
        };
      }}
      class="space-y-4"
    >
      <input type="hidden" name="token" value={data.token} />

      <div class="space-y-2">
        <Label for="name">Full name</Label>
        <Input
          id="name"
          name="name"
          type="text"
          placeholder="Jane Smith"
          bind:value={name}
          required
          autocomplete="name"
        />
      </div>

      <div class="space-y-2">
        <Label for="password">Password</Label>
        <Input
          id="password"
          name="password"
          type="password"
          placeholder="At least 8 characters"
          bind:value={password}
          required
          autocomplete="new-password"
        />
      </div>

      <div class="space-y-2">
        <Label for="confirmPassword">Confirm password</Label>
        <Input
          id="confirmPassword"
          name="confirmPassword"
          type="password"
          placeholder="Repeat your password"
          bind:value={confirmPassword}
          required
          autocomplete="new-password"
        />
      </div>

      <Button type="submit" class="w-full" disabled={submitting}>
        {#if submitting}<Spinner class="size-4 mr-2" />{/if}
        Accept invitation
      </Button>
    </form>
  </div>
</div>
