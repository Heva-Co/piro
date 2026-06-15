<script lang="ts">
  import type { ActionData } from "./$types";

  let { form }: { form: ActionData } = $props();
  let loading = $state(false);
  let name = $state(form?.name ?? "");
  let email = $state(form?.email ?? "");
</script>

<svelte:head>
  <title>Setup — Piro</title>
</svelte:head>

<div class="min-h-screen flex items-center justify-center px-4 bg-background">
  <div class="w-full max-w-sm">
    <div class="text-center mb-8">
      <a href="/" class="text-2xl font-bold tracking-tight">Piro</a>
      <p class="text-muted-foreground text-sm mt-1">Create your owner account to get started</p>
    </div>

    <div class="rounded-2xl border bg-card p-6 shadow-sm">
      {#if form?.error}
        <div class="mb-4 rounded-lg bg-destructive/10 border border-destructive/20 px-4 py-3 text-sm text-destructive">
          {form.error}
        </div>
      {/if}

      <form method="POST" class="flex flex-col gap-4" onsubmit={() => (loading = true)}>
        <div class="flex flex-col gap-1.5">
          <label for="name" class="text-sm font-medium">Your name</label>
          <input
            id="name"
            name="name"
            type="text"
            autocomplete="name"
            required
            bind:value={name}
            placeholder="Jane Smith"
            class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
          />
        </div>

        <div class="flex flex-col gap-1.5">
          <label for="email" class="text-sm font-medium">Email</label>
          <input
            id="email"
            name="email"
            type="email"
            autocomplete="email"
            required
            bind:value={email}
            placeholder="you@example.com"
            class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
          />
        </div>

        <div class="flex flex-col gap-1.5">
          <label for="password" class="text-sm font-medium">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            autocomplete="new-password"
            required
            minlength={8}
            placeholder="Minimum 8 characters"
            class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
          />
        </div>

        <button
          type="submit"
          disabled={loading}
          class="mt-2 w-full rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
        >
          {loading ? "Creating account…" : "Create account & continue"}
        </button>
      </form>
    </div>
  </div>
</div>
