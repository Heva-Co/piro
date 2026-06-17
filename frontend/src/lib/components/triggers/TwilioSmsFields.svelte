<script lang="ts">
  import { Input } from "$lib/components/ui/input/index.js";
  import { Label } from "$lib/components/ui/label/index.js";

  let accountSid = $state("");
  let authToken = $state("");
  let fromNumber = $state("");
  let toNumber = $state("");

  export function getMeta() {
    return { accountSid, authToken, fromNumber, toNumber };
  }

  export function loadMeta(json: string) {
    try {
      const m = JSON.parse(json);
      accountSid = m.accountSid ?? "";
      authToken = m.authToken ?? "";
      fromNumber = m.fromNumber ?? "";
      toNumber = m.toNumber ?? "";
    } catch { /* ignore */ }
  }

  export function validate(): string | null {
    if (!accountSid.trim()) return "Account SID is required.";
    if (!authToken.trim()) return "Auth Token is required.";
    if (!fromNumber.trim()) return "From Number is required.";
    if (!toNumber.trim()) return "To Number is required.";
    return null;
  }
</script>

<div class="space-y-1.5">
  <Label>Account SID <span class="text-destructive">*</span></Label>
  <Input bind:value={accountSid} placeholder="ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" />
  <p class="text-xs text-muted-foreground">Found in your <span class="font-mono">Twilio Console</span> dashboard.</p>
</div>
<div class="space-y-1.5">
  <Label>Auth Token <span class="text-destructive">*</span></Label>
  <Input bind:value={authToken} type="password" placeholder="Your Twilio Auth Token" />
</div>
<div class="space-y-1.5">
  <Label>From Number <span class="text-destructive">*</span></Label>
  <Input bind:value={fromNumber} placeholder="+15551234567" />
  <p class="text-xs text-muted-foreground">Your Twilio phone number in E.164 format.</p>
</div>
<div class="space-y-1.5">
  <Label>To Number <span class="text-destructive">*</span></Label>
  <Input bind:value={toNumber} placeholder="+15559876543" />
  <p class="text-xs text-muted-foreground">Destination phone number in E.164 format.</p>
</div>
<div class="rounded-lg border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
  Messages are sent as a single SMS segment (≤160 chars) with the format:<br />
  <span class="font-mono text-xs">[Piro] CRITICAL: ServiceName/CheckName is Down. 2024-01-01 00:00:00Z</span>
</div>
