<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Card from "$lib/components/ui/card";
  import * as Select from "$lib/components/ui/select";
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import * as patientRemote from "$api/generated/patientRecords.generated.remote";
  import { Plus, Trash2, Syringe } from "lucide-svelte";

  // ── Data loading ──────────────────────────────────────────────────
  // createInsulin and deleteInsulin call getInsulins().refresh() internally,
  // so this reactive query updates automatically after mutations.
  const insulins = patientRemote.getInsulins();

  // ── Category definitions ─────────────────────────────────────────

  const insulinCategories = [
    { value: "RapidActing", label: "Rapid-acting", description: "e.g. Humalog, NovoRapid, Fiasp" },
    { value: "ShortActing", label: "Short-acting", description: "e.g. Humulin R, Actrapid" },
    { value: "IntermediateActing", label: "Intermediate-acting", description: "e.g. Humulin N, Insulatard" },
    { value: "LongActing", label: "Long-acting", description: "e.g. Lantus, Levemir, Tresiba" },
    { value: "UltraLongActing", label: "Ultra long-acting", description: "e.g. Toujeo" },
    { value: "Premixed", label: "Premixed", description: "e.g. NovoMix 30, Humalog Mix" },
  ];

  const categoryLabels: Record<string, string> = Object.fromEntries(
    insulinCategories.map((c) => [c.value, c.label]),
  );

  const categoryDescriptions: Record<string, string> = Object.fromEntries(
    insulinCategories.map((c) => [c.value, c.description]),
  );

  // ── Form state ────────────────────────────────────────────────────

  let showForm = $state(false);
  let insulinCategory = $state("");
  let name = $state("");

  let canAdd = $derived(insulinCategory !== "" && name.trim() !== "");

  const createForm = patientRemote.createInsulin;

  // ── Handlers ──────────────────────────────────────────────────────

  function resetForm() {
    insulinCategory = "";
    name = "";
    showForm = false;
  }

  async function handleDelete(id: string) {
    await patientRemote.deleteInsulin(id);
  }

  async function handleSave(): Promise<boolean> {
    return true;
  }
</script>

<svelte:head>
  <title>Insulins - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Your Insulins"
  description="Add the insulins you use. You can always update these later."
  currentStep={3}
  totalSteps={5}
  prevHref="/settings/setup/devices"
  nextHref="/settings/setup/connectors"
  showSkip={true}
  saveDisabled={false}
  onSave={handleSave}
>
  <!-- Existing insulin cards -->
  {#if insulins.current && insulins.current.length > 0}
    <div class="space-y-3">
      {#each insulins.current as insulin}
        <Card.Root>
          <Card.Header class="flex flex-row items-center gap-3 py-3">
            <div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-muted">
              <Syringe class="h-4 w-4" />
            </div>
            <div class="flex-1 min-w-0">
              <Card.Title class="text-sm font-medium">
                {insulin.name || "Unknown Insulin"}
              </Card.Title>
              <Card.Description class="text-xs">
                {insulin.insulinCategory
                  ? (categoryLabels[insulin.insulinCategory] ?? insulin.insulinCategory)
                  : "Unknown Category"}
                {#if insulin.insulinCategory && categoryDescriptions[insulin.insulinCategory]}
                  &middot; {categoryDescriptions[insulin.insulinCategory]}
                {/if}
              </Card.Description>
            </div>
            {#if insulin.id}
              <Button
                variant="ghost"
                size="icon"
                class="shrink-0 text-muted-foreground hover:text-destructive"
                onclick={() => handleDelete(insulin.id!)}
              >
                <Trash2 class="h-4 w-4" />
              </Button>
            {/if}
          </Card.Header>
        </Card.Root>
      {/each}
    </div>
  {/if}

  <!-- Add insulin button / inline form -->
  {#if showForm}
    <Card.Root>
      <Card.Header>
        <Card.Title class="text-sm">Add an Insulin</Card.Title>
      </Card.Header>
      <form
        {...createForm.enhance(async ({ submit }) => {
          await submit();
          if (createForm.result) resetForm();
        })}
      >
        <Card.Content class="space-y-4">
          <div class="grid gap-4 sm:grid-cols-2">
            <div class="space-y-2">
              <Label for="insulin-category">Category</Label>
              <Select.Root type="single" name="insulinCategory" bind:value={insulinCategory}>
                <Select.Trigger id="insulin-category">
                  {insulinCategory
                    ? (categoryLabels[insulinCategory] ?? insulinCategory)
                    : "Select category"}
                </Select.Trigger>
                <Select.Content>
                  {#each insulinCategories as cat}
                    <Select.Item value={cat.value} label={cat.label}>
                      <div>
                        <div>{cat.label}</div>
                        <div class="text-xs text-muted-foreground">{cat.description}</div>
                      </div>
                    </Select.Item>
                  {/each}
                </Select.Content>
              </Select.Root>
            </div>

            <div class="space-y-2">
              <Label for="insulin-name">Brand / Name</Label>
              <Input
                name="name"
                id="insulin-name"
                bind:value={name}
                placeholder="e.g. Humalog, Lantus"
              />
            </div>
          </div>
          <input type="hidden" name="isCurrent" value="true" />
        </Card.Content>
        <Card.Footer class="flex justify-end gap-2">
          <Button type="button" variant="outline" onclick={resetForm} disabled={!!createForm.pending}>
            Cancel
          </Button>
          <Button type="submit" disabled={!canAdd || !!createForm.pending}>
            {createForm.pending ? "Adding..." : "Add Insulin"}
          </Button>
        </Card.Footer>
      </form>
    </Card.Root>
  {:else}
    <Button variant="outline" onclick={() => (showForm = true)}>
      <Plus class="h-4 w-4 mr-1" />
      Add Insulin
    </Button>
  {/if}
</WizardShell>
