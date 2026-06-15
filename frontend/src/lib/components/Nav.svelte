<script lang="ts">
  import type { UserDto } from "$lib/api";

  let { user }: { user?: UserDto } = $props();
</script>

<header class="border-b bg-background/80 backdrop-blur sticky top-0 z-40">
  <div class="mx-auto max-w-3xl px-4 h-14 flex items-center justify-between">
    <a href="/" class="font-semibold text-base tracking-tight">Piro</a>
    <nav class="flex items-center gap-4 text-sm">
      {#if user}
        <a href="/admin" class="text-muted-foreground hover:text-foreground transition-colors">Admin</a>
        <form method="POST" action="/auth/sign-out">
          <button type="submit" class="text-muted-foreground hover:text-foreground transition-colors">
            Sign out
          </button>
        </form>
      {:else}
        <a href="/auth/sign-in" class="text-muted-foreground hover:text-foreground transition-colors">Sign in</a>
      {/if}
    </nav>
  </div>
</header>
