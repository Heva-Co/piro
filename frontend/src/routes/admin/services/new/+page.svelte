<script lang="ts">
  import { Button } from "$lib/components/ui/button/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import * as Field from "$lib/components/ui/field/index.js";
  import { Input } from "$lib/components/ui/input/index.js";
  import { goto } from "$app/navigation";

  let slug = $state("");
  let name = $state("");
  let description = $state("");
  let displayOrder = $state(0);
  let isHidden = $state(false);
  let saving = $state(false);
  let error = $state("");

  async function create() {
    if (!slug.trim() || !name.trim()) { error = "Slug and name are required."; return; }
    saving = true; error = "";
    try {
      const res = await fetch("/admin/api", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          action: "createService",
          data: { slug: slug.trim(), name: name.trim(), description: description.trim() || null,
            defaultStatus: "NO_DATA", isHidden, displayOrder },
        }),
      });
      const result = await res.json();
      if (result.error) error = result.error;
      else goto(`/admin/services/${slug.trim()}`);
    } catch { error = "Failed to create service."; }
    finally { saving = false; }
  }
</script>


<div class="container mx-auto max-w-xl py-8">
  <div class="mb-6 flex items-center gap-2 text-sm text-muted-foreground">
    <a href="/admin/services" class="hover:text-foreground">Services</a>
    <span>/</span><span>New</span>
  </div>

  <Card.Root>
    <Card.Header><Card.Title>New Service</Card.Title></Card.Header>
    <Card.Content class="flex flex-col gap-4">
      {#if error}<p class="text-destructive text-sm">{error}</p>{/if}
      <div class="grid grid-cols-2 gap-3">
        <Field.Field class="flex flex-col gap-1">
          <Field.Label>Slug *</Field.Label>
          <Input bind:value={slug} placeholder="my-service" class="font-mono" />
        </Field.Field>
        <Field.Field class="flex flex-col gap-1">
          <Field.Label>Name *</Field.Label>
          <Input bind:value={name} placeholder="My Service" />
        </Field.Field>
      </div>
      <Field.Field class="flex flex-col gap-1">
        <Field.Label>Description</Field.Label>
        <Input bind:value={description} placeholder="Optional description" />
      </Field.Field>
      <Field.Field class="flex flex-col gap-1">
        <Field.Label>Display order</Field.Label>
        <Input type="number" bind:value={displayOrder} />
      </Field.Field>
      <label class="flex items-center gap-2 text-sm cursor-pointer">
        <input type="checkbox" bind:checked={isHidden} class="rounded" />
        Hidden from public page
      </label>
      <div class="flex gap-2 pt-2">
        <Button onclick={create} disabled={saving}>{saving ? "Creating…" : "Create Service"}</Button>
        <Button variant="outline" href="/admin/services">Cancel</Button>
      </div>
    </Card.Content>
  </Card.Root>
</div>
