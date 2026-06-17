<script lang="ts">
  import { goto } from "$app/navigation";
  import type { ActionData } from "./$types";

  let { form }: { form: ActionData } = $props();

  // Step 1 state
  let loading = $state(false);
  let name    = $state(form?.name ?? "");
  let email   = $state(form?.email ?? "");

  // Step 2 state
  let step         = $state(1);
  let provider     = $state<"smtp" | "resend">("smtp");
  let smtpHost     = $state("");
  let smtpPort     = $state<number | "">(587);
  let smtpUsername = $state("");
  let smtpPassword = $state("");
  let smtpFrom     = $state("");
  let smtpUseTls   = $state(true);
  let resendApiKey = $state("");
  let resendFrom   = $state("");
  let emailSaving  = $state(false);
  let emailTesting = $state(false);
  let emailError   = $state("");
  let emailSuccess = $state("");

  // Advance to step 2 when account creation succeeds
  $effect(() => {
    if (form?.accountCreated) step = 2;
  });

  async function saveEmail() {
    emailSaving = true; emailError = ""; emailSuccess = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "updateEmailConfig", data: {
          provider,
          smtpHost: smtpHost || null,
          smtpPort: smtpPort !== "" ? Number(smtpPort) : null,
          smtpUsername: smtpUsername || null,
          smtpPassword: smtpPassword || null,
          smtpFrom: smtpFrom || null,
          smtpUseTls,
          resendApiKey: resendApiKey || null,
          resendFrom: resendFrom || null,
        }}),
      });
      const data = await res.json();
      if (data.error) { emailError = data.error; return; }
      goto("/admin");
    } catch { emailError = "Failed to save email configuration."; }
    finally { emailSaving = false; }
  }

  async function testEmail() {
    emailTesting = true; emailError = ""; emailSuccess = "";
    try {
      // Save current config first so the test uses the provider shown on screen
      const saveRes = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "updateEmailConfig", data: {
          provider,
          smtpHost: smtpHost || null,
          smtpPort: smtpPort !== "" ? Number(smtpPort) : null,
          smtpUsername: smtpUsername || null,
          smtpPassword: smtpPassword || null,
          smtpFrom: smtpFrom || null,
          smtpUseTls,
          resendApiKey: resendApiKey || null,
          resendFrom: resendFrom || null,
        }}),
      });
      const saved = await saveRes.json();
      if (saved.error) { emailError = saved.error; return; }

      const res = await fetch("/admin/api", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: "testEmailConfig" }),
      });
      const data = await res.json();
      if (data.error) emailError = data.error;
      else emailSuccess = data.message ?? "Test email sent.";
    } catch { emailError = "Failed to send test email."; }
    finally { emailTesting = false; }
  }
</script>

<svelte:head>
  <title>Setup — Piro</title>
</svelte:head>

<div class="min-h-screen flex items-center justify-center px-4 bg-background">
  <div class="w-full max-w-sm">
    <div class="text-center mb-8">
      <a href="/" class="text-2xl font-bold tracking-tight">Piro</a>
      {#if step === 1}
        <p class="text-muted-foreground text-sm mt-1">Create your owner account to get started</p>
      {:else}
        <p class="text-muted-foreground text-sm mt-1">Configure your email provider</p>
      {/if}
    </div>

    <!-- Step indicator -->
    <div class="flex items-center gap-2 mb-6 justify-center">
      <div class="flex items-center gap-1.5 text-xs">
        <span class="size-5 rounded-full flex items-center justify-center text-[10px] font-semibold {step === 1 ? 'bg-primary text-primary-foreground' : 'bg-primary/20 text-primary'}">1</span>
        <span class={step === 1 ? "font-medium" : "text-muted-foreground"}>Account</span>
      </div>
      <div class="h-px w-6 bg-border"></div>
      <div class="flex items-center gap-1.5 text-xs">
        <span class="size-5 rounded-full flex items-center justify-center text-[10px] font-semibold {step === 2 ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'}">2</span>
        <span class={step === 2 ? "font-medium" : "text-muted-foreground"}>Email</span>
      </div>
    </div>

    <div class="rounded-2xl border bg-card p-6 shadow-sm">

      {#if step === 1}
        {#if form?.error}
          <div class="mb-4 rounded-lg bg-destructive/10 border border-destructive/20 px-4 py-3 text-sm text-destructive">
            {form.error}
          </div>
        {/if}

        <form method="POST" class="flex flex-col gap-4" onsubmit={() => (loading = true)}>
          <div class="flex flex-col gap-1.5">
            <label for="name" class="text-sm font-medium">Your name</label>
            <input id="name" name="name" type="text" autocomplete="name" required bind:value={name}
              placeholder="Jane Smith"
              class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="email" class="text-sm font-medium">Email</label>
            <input id="email" name="email" type="email" autocomplete="email" required bind:value={email}
              placeholder="you@example.com"
              class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="password" class="text-sm font-medium">Password</label>
            <input id="password" name="password" type="password" autocomplete="new-password" required minlength={8}
              placeholder="Minimum 8 characters"
              class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <button type="submit" disabled={loading}
            class="mt-2 w-full rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity">
            {loading ? "Creating account…" : "Create account & continue"}
          </button>
        </form>

      {:else}
        {#if emailError}
          <div class="mb-4 rounded-lg bg-destructive/10 border border-destructive/20 px-4 py-3 text-sm text-destructive">
            {emailError}
          </div>
        {/if}
        {#if emailSuccess}
          <div class="mb-4 rounded-lg bg-green-50 border border-green-200 px-4 py-3 text-sm text-green-800 dark:bg-green-950 dark:border-green-800 dark:text-green-300">
            {emailSuccess}
          </div>
        {/if}

        <div class="flex flex-col gap-4">
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium">Provider</label>
            <select bind:value={provider} class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring">
              <option value="smtp">SMTP</option>
              <option value="resend">Resend</option>
            </select>
          </div>

          {#if provider === "smtp"}
            <div class="grid grid-cols-2 gap-2">
              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-medium">Host</label>
                <input bind:value={smtpHost} placeholder="smtp.example.com"
                  class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
              </div>
              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-medium">Port</label>
                <input bind:value={smtpPort} type="number" placeholder="587"
                  class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
              </div>
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium">Username</label>
              <input bind:value={smtpUsername} placeholder="user@example.com" autocomplete="off"
                class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium">Password</label>
              <input bind:value={smtpPassword} type="password" placeholder="SMTP password" autocomplete="new-password"
                class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium">From address</label>
              <input bind:value={smtpFrom} placeholder="no-reply@example.com"
                class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
            </div>
          {/if}

          {#if provider === "resend"}
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium">API Key</label>
              <input bind:value={resendApiKey} type="password" placeholder="re_..." autocomplete="new-password"
                class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium">From address</label>
              <input bind:value={resendFrom} placeholder="no-reply@yourdomain.com"
                class="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring" />
            </div>
          {/if}

          <button onclick={testEmail} disabled={emailTesting || emailSaving} type="button"
            class="w-full rounded-lg border py-2.5 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors">
            {emailTesting ? "Sending…" : "Send test email"}
          </button>
          <button onclick={saveEmail} disabled={emailSaving || emailTesting} type="button"
            class="w-full rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity">
            {emailSaving ? "Saving…" : "Save & go to dashboard"}
          </button>
          <button onclick={() => goto("/admin")} type="button"
            class="w-full text-center text-sm text-muted-foreground hover:text-foreground transition-colors">
            Skip for now
          </button>
        </div>
      {/if}

    </div>
  </div>
</div>
