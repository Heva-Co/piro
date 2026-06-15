<script lang="ts">
  import { onMount } from "svelte";
  import * as Card from "$lib/components/ui/card/index.js";
  import { Button } from "$lib/components/ui/button/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { Textarea } from "$lib/components/ui/textarea/index.js";
  import { Label } from "$lib/components/ui/label/index.js";
  import { Spinner } from "$lib/components/ui/spinner/index.js";
  import { toast } from "svelte-sonner";
  import { PIRO_API } from "$lib/api.js";
  import UploadIcon from "@lucide/svelte/icons/upload";
  import XIcon from "@lucide/svelte/icons/x";
  import SaveIcon from "@lucide/svelte/icons/save";

  // ── State ──────────────────────────────────────────────────────────────────
  let loading  = $state(true);
  let savingInfo    = $state(false);
  let savingLogo    = $state(false);
  let savingFavicon = $state(false);
  let savingOg      = $state(false);

  // Site info
  let siteName = $state("");
  let siteUrl  = $state("");

  // Branding
  let logoUrl    = $state<string | null>(null);
  let faviconUrl = $state<string | null>(null);

  // SEO
  let metaTitle       = $state("");
  let metaDescription = $state("");
  let ogImageUrl      = $state<string | null>(null);

  // ── Helpers ────────────────────────────────────────────────────────────────
  async function adminApi(action: string, data?: object) {
    const res = await fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action, data }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  }

  async function uploadFile(type: "logo" | "favicon" | "og-image", file: File): Promise<string> {
    const form = new FormData();
    form.append("type", type);
    form.append("file", file);
    const res = await fetch("/admin/site-upload", { method: "POST", body: form });
    const result = await res.json();
    if (result.error || !res.ok) throw new Error(result.error ?? "Upload failed");
    return result.url as string;
  }

  function pickFile(accept: string): Promise<File | null> {
    return new Promise((resolve) => {
      const input = document.createElement("input");
      input.type = "file";
      input.accept = accept;
      input.onchange = () => resolve(input.files?.[0] ?? null);
      input.click();
    });
  }

  // ── Load ───────────────────────────────────────────────────────────────────
  async function load() {
    try {
      const cfg = await adminApi("getSiteConfig");
      siteName        = cfg.name        ?? "";
      siteUrl         = cfg.url         ?? "";
      logoUrl         = cfg.logoUrl     ?? null;
      faviconUrl      = cfg.faviconUrl  ?? null;
      metaTitle       = cfg.metaTitle   ?? "";
      metaDescription = cfg.metaDescription ?? "";
      ogImageUrl      = cfg.ogImageUrl  ?? null;
    } catch {
      toast.error("Failed to load site configuration.");
    } finally {
      loading = false;
    }
  }

  // ── Save handlers ──────────────────────────────────────────────────────────
  async function saveSiteInfo() {
    savingInfo = true;
    try {
      await adminApi("updateSiteConfig", { name: siteName || null, url: siteUrl || null,
        metaTitle: metaTitle || null, metaDescription: metaDescription || null });
      toast.success("Site information saved.");
    } catch { toast.error("Failed to save site information."); }
    finally { savingInfo = false; }
  }

  async function handleLogoUpload() {
    const file = await pickFile("image/png,image/jpeg,image/svg+xml,image/webp");
    if (!file) return;
    savingLogo = true;
    try {
      logoUrl = await uploadFile("logo", file);
      toast.success("Logo uploaded.");
    } catch (e: unknown) { toast.error(e instanceof Error ? e.message : "Upload failed."); }
    finally { savingLogo = false; }
  }

  async function clearLogo() {
    savingLogo = true;
    try {
      await adminApi("deleteSiteAsset", { type: "logo" });
      logoUrl = null;
      toast.success("Logo removed.");
    } catch { toast.error("Failed to remove logo."); }
    finally { savingLogo = false; }
  }

  async function handleFaviconUpload() {
    const file = await pickFile("image/png,image/jpeg,image/svg+xml,image/webp");
    if (!file) return;
    savingFavicon = true;
    try {
      faviconUrl = await uploadFile("favicon", file);
      toast.success("Favicon uploaded.");
    } catch (e: unknown) { toast.error(e instanceof Error ? e.message : "Upload failed."); }
    finally { savingFavicon = false; }
  }

  async function clearFavicon() {
    savingFavicon = true;
    try {
      await adminApi("deleteSiteAsset", { type: "favicon" });
      faviconUrl = null;
      toast.success("Favicon removed.");
    } catch { toast.error("Failed to remove favicon."); }
    finally { savingFavicon = false; }
  }

  async function handleOgUpload() {
    const file = await pickFile("image/png,image/jpeg,image/webp");
    if (!file) return;
    savingOg = true;
    try {
      ogImageUrl = await uploadFile("og-image", file);
      toast.success("Social preview image uploaded.");
    } catch (e: unknown) { toast.error(e instanceof Error ? e.message : "Upload failed."); }
    finally { savingOg = false; }
  }

  async function clearOgImage() {
    savingOg = true;
    try {
      await adminApi("deleteSiteAsset", { type: "og-image" });
      ogImageUrl = null;
      toast.success("Social preview image removed.");
    } catch { toast.error("Failed to remove social preview image."); }
    finally { savingOg = false; }
  }

  onMount(load);
</script>


<div class="flex w-full flex-col gap-4 p-4">
  <div>
    <h1 class="text-2xl font-bold">Site</h1>
    <p class="text-sm text-muted-foreground mt-1">Customize your status page identity and SEO settings.</p>
  </div>

  {#if loading}
    <div class="flex justify-center py-16"><Spinner class="size-6" /></div>
  {:else}

    <!-- Site Information -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Site Information</Card.Title>
        <Card.Description>Basic information about your status page</Card.Description>
      </Card.Header>
      <Card.Content class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div class="space-y-1.5">
          <Label>Site Name</Label>
          <Input bind:value={siteName} placeholder="Acme Status" />
          <p class="text-xs text-muted-foreground">The name displayed in the header and browser tab</p>
        </div>
        <div class="space-y-1.5">
          <Label>Site URL</Label>
          <Input bind:value={siteUrl} placeholder="https://status.example.com" type="url" />
          <p class="text-xs text-muted-foreground">Used in email notifications and links</p>
        </div>
      </Card.Content>
      <Card.Footer class="justify-end">
        <Button onclick={saveSiteInfo} disabled={savingInfo}>
          {#if savingInfo}<Spinner class="size-4 mr-2" />{:else}<SaveIcon class="size-4 mr-2" />{/if}
          Save
        </Button>
      </Card.Footer>
    </Card.Root>

    <!-- Logo -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Logo</Card.Title>
        <Card.Description>Upload your site logo (max 512 KB, PNG/JPG/SVG/WebP)</Card.Description>
      </Card.Header>
      <Card.Content class="flex items-center gap-4">
        {#if logoUrl}
          <img src="{PIRO_API}{logoUrl}" alt="Logo" class="h-14 w-14 rounded-lg object-contain border bg-muted" />
          <div class="flex flex-col gap-1">
            <p class="text-xs text-muted-foreground font-mono">{logoUrl}</p>
            <div class="flex gap-2">
              <Button variant="outline" size="sm" onclick={handleLogoUpload} disabled={savingLogo}>
                {#if savingLogo}<Spinner class="size-4 mr-1" />{:else}<UploadIcon class="size-4 mr-1" />{/if}
                Replace
              </Button>
              <Button variant="ghost" size="sm" onclick={clearLogo} disabled={savingLogo}>
                <XIcon class="size-4 mr-1" /> Remove
              </Button>
            </div>
          </div>
        {:else}
          <img src="/piro.svg" alt="Default logo" class="h-14 w-14 rounded-lg object-contain border bg-muted opacity-40" />
          <div class="flex flex-col gap-1">
            <p class="text-xs text-muted-foreground">Using default logo</p>
            <Button variant="outline" onclick={handleLogoUpload} disabled={savingLogo}>
              {#if savingLogo}<Spinner class="size-4 mr-2" />{:else}<UploadIcon class="size-4 mr-2" />{/if}
              Upload Logo
            </Button>
          </div>
        {/if}
      </Card.Content>
    </Card.Root>

    <!-- Favicon -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Favicon</Card.Title>
        <Card.Description>Upload your site favicon (max 512 KB, PNG/JPG/SVG/WebP, recommended 64×64px)</Card.Description>
      </Card.Header>
      <Card.Content class="flex items-center gap-4">
        {#if faviconUrl}
          <img src="{PIRO_API}{faviconUrl}" alt="Favicon" class="h-10 w-10 rounded object-contain border bg-muted" />
          <div class="flex flex-col gap-1">
            <p class="text-xs text-muted-foreground font-mono">{faviconUrl}</p>
            <div class="flex gap-2">
              <Button variant="outline" size="sm" onclick={handleFaviconUpload} disabled={savingFavicon}>
                {#if savingFavicon}<Spinner class="size-4 mr-1" />{:else}<UploadIcon class="size-4 mr-1" />{/if}
                Replace
              </Button>
              <Button variant="ghost" size="sm" onclick={clearFavicon} disabled={savingFavicon}>
                <XIcon class="size-4 mr-1" /> Remove
              </Button>
            </div>
          </div>
        {:else}
          <img src="/favicon-32x32.png" alt="Default favicon" class="h-10 w-10 rounded object-contain border bg-muted opacity-40" />
          <div class="flex flex-col gap-1">
            <p class="text-xs text-muted-foreground">Using default favicon</p>
            <Button variant="outline" onclick={handleFaviconUpload} disabled={savingFavicon}>
              {#if savingFavicon}<Spinner class="size-4 mr-2" />{:else}<UploadIcon class="size-4 mr-2" />{/if}
              Upload Favicon
            </Button>
          </div>
        {/if}
      </Card.Content>
    </Card.Root>

    <!-- Social Preview & SEO -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Social Preview & SEO</Card.Title>
        <Card.Description>Configure the social preview image and meta tags for search engines</Card.Description>
      </Card.Header>
      <Card.Content class="space-y-4">
        <!-- OG image -->
        <div class="flex items-start gap-4">
          {#if ogImageUrl}
            <img src="{PIRO_API}{ogImageUrl}" alt="Social preview"
              class="h-24 w-40 rounded-lg object-cover border bg-muted flex-shrink-0" />
            <div class="flex flex-col gap-1">
              <p class="text-xs text-muted-foreground font-mono">{ogImageUrl}</p>
              <div class="flex gap-2">
                <Button variant="outline" size="sm" onclick={handleOgUpload} disabled={savingOg}>
                  {#if savingOg}<Spinner class="size-4 mr-1" />{:else}<UploadIcon class="size-4 mr-1" />{/if}
                  Replace
                </Button>
                <Button variant="ghost" size="sm" onclick={clearOgImage} disabled={savingOg}>
                  <XIcon class="size-4 mr-1" /> Remove
                </Button>
              </div>
            </div>
          {:else}
            <div class="flex h-24 w-40 flex-shrink-0 items-center justify-center rounded-lg border bg-muted text-muted-foreground">
              <UploadIcon class="size-8" />
            </div>
            <div class="space-y-1">
              <Button variant="outline" onclick={handleOgUpload} disabled={savingOg}>
                {#if savingOg}<Spinner class="size-4 mr-2" />{:else}<UploadIcon class="size-4 mr-2" />{/if}
                Upload Social Preview
              </Button>
              <p class="text-xs text-muted-foreground">Optional. Leave empty to use no social preview image. (max 2 MB)</p>
            </div>
          {/if}
        </div>

        <div class="space-y-1.5">
          <Label>Meta Title</Label>
          <Input bind:value={metaTitle} placeholder={siteName || "Acme Status"} />
          <p class="text-xs text-muted-foreground">Overrides the default page title in search results</p>
        </div>

        <div class="space-y-1.5">
          <Label>Meta Description</Label>
          <Textarea bind:value={metaDescription} placeholder="Real-time status and uptime for Acme services." class="min-h-20 resize-none" />
          <p class="text-xs text-muted-foreground">Shown as the snippet text in search engine results</p>
        </div>
      </Card.Content>
      <Card.Footer class="justify-end">
        <Button onclick={saveSiteInfo} disabled={savingInfo}>
          {#if savingInfo}<Spinner class="size-4 mr-2" />{:else}<SaveIcon class="size-4 mr-2" />{/if}
          Save
        </Button>
      </Card.Footer>
    </Card.Root>

  {/if}
</div>
