<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Badge } from "$lib/components/ui/badge";
  import * as Card from "$lib/components/ui/card";
  import * as Select from "$lib/components/ui/select";
  import * as Dialog from "$lib/components/ui/dialog";
  import * as AlertDialog from "$lib/components/ui/alert-dialog";
  import { Textarea } from "$lib/components/ui/textarea";
  import {
    Syringe,
    Plus,
    Pencil,
    Trash2,
    Save,
    Loader2,
  } from "lucide-svelte";
  import {
    type PatientInsulin,
    type InsulinFormulation,
    InsulinCategory,
    InsulinRole,
  } from "$api";
  import {
    insulinCategoryLabels,
    insulinCategoryDescriptions,
    insulinRoleLabels,
    insulinRoleDescriptions,
  } from "./labels";
  import { createInsulinListState } from "./state.svelte";

  interface Props {
    /** "inline" = wizard-style card forms, "dialog" = settings-style dialog CRUD */
    variant?: "inline" | "dialog";
  }

  let { variant = "dialog" }: Props = $props();

  const insulinList = createInsulinListState();

  // ── Helpers ──────────────────────────────────────────────────────

  function formatDate(date: Date | string | undefined): string {
    if (!date) return "";
    const d = new Date(date);
    return d.toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  }

  /** Get formulations matching the selected category */
  function formulationsForCategory(category: string): InsulinFormulation[] {
    if (!category) return [];
    return insulinList.catalog.filter((f) => f.category === category);
  }

  /** Apply formulation defaults to form fields */
  function applyFormulation(formulation: InsulinFormulation) {
    return {
      name: formulation.name ?? "",
      dia: formulation.defaultDia ?? 4.0,
      peak: formulation.defaultPeak ?? 75,
      curve: formulation.curve ?? "rapid-acting",
      concentration: formulation.concentration ?? 100,
    };
  }

  // ── Inline variant state ────────────────────────────────────────

  let showInlineForm = $state(false);
  let inlineCategory = $state("");
  let inlineFormulationId = $state("");
  let inlineName = $state("");
  let inlineDia = $state(4.0);
  let inlinePeak = $state(75);
  let inlineCurve = $state("rapid-acting");
  let inlineConcentration = $state(100);
  let inlineRole = $state<string>(InsulinRole.Bolus);

  let inlineFormulations = $derived(formulationsForCategory(inlineCategory));

  let canAddInline = $derived(inlineCategory !== "" && inlineName.trim() !== "");

  function onInlineCategoryChange() {
    // Reset formulation when category changes
    inlineFormulationId = "";
    inlineName = "";
    // Set defaults based on category
    const isLongActing = inlineCategory === InsulinCategory.LongActing
      || inlineCategory === InsulinCategory.UltraLongActing;
    inlineRole = isLongActing ? InsulinRole.Basal : InsulinRole.Bolus;
    inlineDia = isLongActing ? 24.0 : 4.0;
    inlinePeak = isLongActing ? 0 : 75;
    inlineCurve = isLongActing ? "bilinear" : "rapid-acting";
  }

  function onInlineFormulationChange() {
    const formulation = insulinList.catalog.find((f) => f.id === inlineFormulationId);
    if (formulation) {
      const defaults = applyFormulation(formulation);
      inlineName = defaults.name;
      inlineDia = defaults.dia;
      inlinePeak = defaults.peak;
      inlineCurve = defaults.curve;
      inlineConcentration = defaults.concentration;
    }
  }

  function resetInlineForm() {
    inlineCategory = "";
    inlineFormulationId = "";
    inlineName = "";
    inlineDia = 4.0;
    inlinePeak = 75;
    inlineCurve = "rapid-acting";
    inlineConcentration = 100;
    inlineRole = InsulinRole.Bolus;
    showInlineForm = false;
  }

  // ── Dialog variant state ────────────────────────────────────────

  let dialogOpen = $state(false);
  let editing = $state<PatientInsulin | null>(null);
  let deleteId = $state<string | null>(null);

  let insulinCategory = $state<string>(InsulinCategory.RapidActing);
  let insulinFormulationId = $state("");
  let insulinName = $state("");
  let insulinStartDate = $state("");
  let insulinEndDate = $state("");
  let insulinIsCurrent = $state(true);
  let insulinNotes = $state("");
  let insulinDia = $state(4.0);
  let insulinPeak = $state(75);
  let insulinCurve = $state("rapid-acting");
  let insulinConcentration = $state(100);
  let insulinRole = $state<string>(InsulinRole.Bolus);
  let insulinIsPrimary = $state(false);

  let dialogFormulations = $derived(formulationsForCategory(insulinCategory));

  const activeForm = $derived(editing?.id ? insulinList.updateForm : insulinList.createForm);
  const dialogSaving = $derived(!!insulinList.createForm.pending || !!insulinList.updateForm.pending);

  function onDialogCategoryChange() {
    insulinFormulationId = "";
    insulinName = "";
    const isLongActing = insulinCategory === InsulinCategory.LongActing
      || insulinCategory === InsulinCategory.UltraLongActing;
    insulinRole = isLongActing ? InsulinRole.Basal : InsulinRole.Bolus;
    insulinDia = isLongActing ? 24.0 : 4.0;
    insulinPeak = isLongActing ? 0 : 75;
    insulinCurve = isLongActing ? "bilinear" : "rapid-acting";
  }

  function onDialogFormulationChange() {
    const formulation = insulinList.catalog.find((f) => f.id === insulinFormulationId);
    if (formulation) {
      const defaults = applyFormulation(formulation);
      insulinName = defaults.name;
      insulinDia = defaults.dia;
      insulinPeak = defaults.peak;
      insulinCurve = defaults.curve;
      insulinConcentration = defaults.concentration;
    }
  }

  function openDialog(insulin?: PatientInsulin) {
    if (insulin) {
      editing = insulin;
      insulinCategory = insulin.insulinCategory ?? InsulinCategory.RapidActing;
      insulinFormulationId = insulin.formulationId ?? "";
      insulinName = insulin.name ?? "";
      insulinStartDate = insulin.startDate
        ? new Date(insulin.startDate).toISOString().split("T")[0]
        : "";
      insulinEndDate = insulin.endDate
        ? new Date(insulin.endDate).toISOString().split("T")[0]
        : "";
      insulinIsCurrent = insulin.isCurrent ?? true;
      insulinNotes = insulin.notes ?? "";
      insulinDia = insulin.dia ?? 4.0;
      insulinPeak = insulin.peak ?? 75;
      insulinCurve = insulin.curve ?? "rapid-acting";
      insulinConcentration = insulin.concentration ?? 100;
      insulinRole = insulin.role ?? InsulinRole.Bolus;
      insulinIsPrimary = insulin.isPrimary ?? false;
    } else {
      editing = null;
      insulinCategory = InsulinCategory.RapidActing;
      insulinFormulationId = "";
      insulinName = "";
      insulinStartDate = "";
      insulinEndDate = "";
      insulinIsCurrent = true;
      insulinNotes = "";
      insulinDia = 4.0;
      insulinPeak = 75;
      insulinCurve = "rapid-acting";
      insulinConcentration = 100;
      insulinRole = InsulinRole.Bolus;
      insulinIsPrimary = false;
    }
    dialogOpen = true;
  }

  async function handleDelete() {
    if (!deleteId) return;
    await insulinList.remove(deleteId);
    deleteId = null;
  }

  // ── Category items with descriptions ─────────────────────────

  const insulinCategoryItems = $derived(
    Object.entries(insulinCategoryLabels).map(([value, label]) => ({
      value,
      label,
      description: insulinCategoryDescriptions[value] ?? "",
    })),
  );
</script>

{#if variant === "inline"}
  <!-- ── Inline variant (wizard-style) ─────────────────────────── -->

  {#if insulinList.items.length > 0}
    <div class="space-y-3">
      {#each insulinList.items as insulin}
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
                  ? (insulinCategoryLabels[insulin.insulinCategory] ?? insulin.insulinCategory)
                  : "Unknown Category"}
                {#if insulin.role}
                  &middot; {insulinRoleLabels[insulin.role] ?? insulin.role}
                {/if}
                {#if insulin.dia}
                  &middot; DIA {insulin.dia}h
                {/if}
                {#if insulin.isPrimary}
                  &middot; Primary
                {/if}
              </Card.Description>
            </div>
            {#if insulin.id}
              <Button
                variant="ghost"
                size="icon"
                class="shrink-0 text-muted-foreground hover:text-destructive"
                onclick={() => insulinList.remove(insulin.id!)}
              >
                <Trash2 class="h-4 w-4" />
              </Button>
            {/if}
          </Card.Header>
        </Card.Root>
      {/each}
    </div>
  {/if}

  {#if showInlineForm}
    <Card.Root>
      <Card.Header>
        <Card.Title class="text-sm">Add an Insulin</Card.Title>
      </Card.Header>
      <form
        {...insulinList.createForm.enhance(async ({ submit }) => {
          await submit();
          if (insulinList.createForm.result) resetInlineForm();
        })}
      >
        <Card.Content class="space-y-4">
          <div class="grid gap-4 sm:grid-cols-2">
            <div class="space-y-2">
              <Label for="insulin-category">Category</Label>
              <Select.Root
                type="single"
                name="insulinCategory"
                bind:value={inlineCategory}
                onValueChange={() => onInlineCategoryChange()}
              >
                <Select.Trigger id="insulin-category">
                  {inlineCategory
                    ? (insulinCategoryLabels[inlineCategory] ?? inlineCategory)
                    : "Select category"}
                </Select.Trigger>
                <Select.Content>
                  {#each insulinCategoryItems as cat}
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

            {#if inlineCategory && inlineFormulations.length > 0}
              <div class="space-y-2">
                <Label for="insulin-formulation">Formulation</Label>
                <Select.Root
                  type="single"
                  bind:value={inlineFormulationId}
                  onValueChange={() => onInlineFormulationChange()}
                >
                  <Select.Trigger id="insulin-formulation">
                    {inlineFormulationId
                      ? (insulinList.catalog.find(f => f.id === inlineFormulationId)?.name ?? "Select formulation")
                      : "Select formulation"}
                  </Select.Trigger>
                  <Select.Content>
                    {#each inlineFormulations as f}
                      <Select.Item value={f.id ?? ""} label={f.name ?? ""}>
                        <div>
                          <div>{f.name}</div>
                          <div class="text-xs text-muted-foreground">
                            DIA {f.defaultDia}h &middot; U-{f.concentration}
                          </div>
                        </div>
                      </Select.Item>
                    {/each}
                    <Select.Item value="" label="Custom">
                      <div>
                        <div>Custom</div>
                        <div class="text-xs text-muted-foreground">Enter a custom insulin name</div>
                      </div>
                    </Select.Item>
                  </Select.Content>
                </Select.Root>
              </div>
            {/if}

            <div class="space-y-2">
              <Label for="insulin-name">Brand / Name</Label>
              <Input
                name="name"
                id="insulin-name"
                bind:value={inlineName}
                placeholder="e.g. Humalog, Lantus"
                disabled={!!inlineFormulationId}
              />
            </div>

            <div class="space-y-2">
              <Label for="insulin-role">Role</Label>
              <Select.Root type="single" name="role" bind:value={inlineRole}>
                <Select.Trigger id="insulin-role">
                  {insulinRoleLabels[inlineRole] ?? inlineRole}
                </Select.Trigger>
                <Select.Content>
                  {#each Object.entries(insulinRoleLabels) as [value, label]}
                    <Select.Item {value} {label}>
                      <div>
                        <div>{label}</div>
                        <div class="text-xs text-muted-foreground">
                          {insulinRoleDescriptions[value] ?? ""}
                        </div>
                      </div>
                    </Select.Item>
                  {/each}
                </Select.Content>
              </Select.Root>
            </div>
          </div>

          <div class="grid gap-4 sm:grid-cols-3">
            <div class="space-y-2">
              <Label for="insulin-dia">Duration of Insulin Action</Label>
              <div class="flex items-center gap-2">
                <Input
                  id="insulin-dia"
                  type="number"
                  bind:value={inlineDia}
                  step={0.5}
                  min={0.5}
                  max={48}
                  class="flex-1"
                />
                <span class="text-sm text-muted-foreground whitespace-nowrap">hours</span>
              </div>
            </div>

            <div class="space-y-2">
              <Label for="insulin-peak">Peak</Label>
              <div class="flex items-center gap-2">
                <Input
                  id="insulin-peak"
                  type="number"
                  bind:value={inlinePeak}
                  step={5}
                  min={0}
                  class="flex-1"
                />
                <span class="text-sm text-muted-foreground whitespace-nowrap">min</span>
              </div>
            </div>

            <div class="space-y-2">
              <Label for="insulin-concentration">Concentration</Label>
              <div class="flex items-center gap-2">
                <span class="text-sm text-muted-foreground whitespace-nowrap">U-</span>
                <Input
                  id="insulin-concentration"
                  type="number"
                  bind:value={inlineConcentration}
                  step={100}
                  min={100}
                  class="flex-1"
                />
              </div>
            </div>
          </div>

          <!-- Hidden fields for form submission -->
          <input type="hidden" name="b:isCurrent" value="on" />
          <input type="hidden" name="n:dia" value={inlineDia} />
          <input type="hidden" name="n:peak" value={inlinePeak} />
          <input type="hidden" name="curve" value={inlineCurve} />
          <input type="hidden" name="n:concentration" value={inlineConcentration} />
          <input type="hidden" name="b:isPrimary" value={insulinList.items.length === 0 ? "on" : ""} />
          {#if inlineFormulationId}
            <input type="hidden" name="formulationId" value={inlineFormulationId} />
          {/if}

          {#each insulinList.createForm.fields.allIssues() as issue}
            <p class="text-sm text-destructive">{issue.message}</p>
          {/each}
        </Card.Content>
        <Card.Footer class="flex justify-end gap-2">
          <Button type="button" variant="outline" onclick={resetInlineForm} disabled={!!insulinList.createForm.pending}>
            Cancel
          </Button>
          <Button type="submit" disabled={!canAddInline || !!insulinList.createForm.pending}>
            {insulinList.createForm.pending ? "Adding..." : "Add Insulin"}
          </Button>
        </Card.Footer>
      </form>
    </Card.Root>
  {:else}
    <Button variant="outline" onclick={() => (showInlineForm = true)}>
      <Plus class="h-4 w-4 mr-1" />
      Add Insulin
    </Button>
  {/if}

{:else}
  <!-- ── Dialog variant (settings-style) ───────────────────────── -->

  {#if insulinList.items.length === 0}
    <p class="text-sm text-muted-foreground py-4 text-center">
      No insulins added yet. Add your first insulin to get started.
    </p>
  {:else}
    <div class="space-y-3">
      {#each insulinList.items as insulin}
        <div
          class="flex items-center justify-between rounded-lg border p-3"
        >
          <div class="space-y-1 min-w-0 flex-1">
            <div class="flex items-center gap-2 flex-wrap">
              <span class="font-medium text-sm">
                {insulin.name ?? "Unnamed"}
              </span>
              <Badge variant="secondary" class="text-xs">
                {insulinCategoryLabels[insulin.insulinCategory ?? ""] ??
                  insulin.insulinCategory}
              </Badge>
              {#if insulin.role}
                <Badge variant="outline" class="text-xs">
                  {insulinRoleLabels[insulin.role] ?? insulin.role}
                </Badge>
              {/if}
              {#if insulin.isCurrent}
                <Badge
                  variant="default"
                  class="text-xs bg-green-600 hover:bg-green-700"
                >
                  Current
                </Badge>
              {/if}
              {#if insulin.isPrimary}
                <Badge
                  variant="default"
                  class="text-xs"
                >
                  Primary
                </Badge>
              {/if}
            </div>
            <div class="flex items-center gap-3 text-xs text-muted-foreground flex-wrap">
              {#if insulin.dia}
                <span>DIA {insulin.dia}h</span>
              {/if}
              {#if insulin.startDate}
                <span>From {formatDate(insulin.startDate)}</span>
              {/if}
              {#if insulin.endDate}
                <span>Until {formatDate(insulin.endDate)}</span>
              {/if}
            </div>
          </div>
          <div class="flex items-center gap-1 ml-2">
            <Button
              variant="ghost"
              size="icon"
              onclick={() => openDialog(insulin)}
            >
              <Pencil class="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onclick={() => (deleteId = insulin.id ?? null)}
            >
              <Trash2 class="h-4 w-4 text-destructive" />
            </Button>
          </div>
        </div>
      {/each}
    </div>
  {/if}

  <!-- Add button for dialog variant -->
  <div class="pt-2">
    <Button size="sm" onclick={() => openDialog()}>
      <Plus class="mr-2 h-4 w-4" />
      Add Insulin
    </Button>
  </div>

  <!-- Insulin Dialog -->
  <Dialog.Root bind:open={dialogOpen}>
    <Dialog.Content class="sm:max-w-lg max-h-[90vh] overflow-y-auto">
      <Dialog.Header>
        <Dialog.Title>
          {editing ? "Edit Insulin" : "Add Insulin"}
        </Dialog.Title>
        <Dialog.Description>
          {editing
            ? "Update the details of this insulin."
            : "Add an insulin to your patient record."}
        </Dialog.Description>
      </Dialog.Header>

      <form
        {...activeForm.enhance(async ({ submit }) => {
          await submit();
          if (activeForm.result) dialogOpen = false;
        })}
      >
        {#if editing?.id}
          <input type="hidden" name="id" value={editing.id} />
        {/if}
        <div class="space-y-4 py-4">
          <div class="space-y-2">
            <Label for="insulin-category">Category</Label>
            <Select.Root
              type="single"
              name="insulinCategory"
              bind:value={insulinCategory}
              onValueChange={() => onDialogCategoryChange()}
            >
              <Select.Trigger id="insulin-category">
                {insulinCategoryLabels[insulinCategory] ?? insulinCategory}
              </Select.Trigger>
              <Select.Content>
                {#each Object.entries(insulinCategoryLabels) as [value, label]}
                  <Select.Item {value} {label} />
                {/each}
              </Select.Content>
            </Select.Root>
          </div>

          {#if dialogFormulations.length > 0}
            <div class="space-y-2">
              <Label for="insulin-formulation">Formulation</Label>
              <Select.Root
                type="single"
                bind:value={insulinFormulationId}
                onValueChange={() => onDialogFormulationChange()}
              >
                <Select.Trigger id="insulin-formulation">
                  {insulinFormulationId
                    ? (insulinList.catalog.find(f => f.id === insulinFormulationId)?.name ?? "Custom")
                    : "Select formulation or enter custom"}
                </Select.Trigger>
                <Select.Content>
                  {#each dialogFormulations as f}
                    <Select.Item value={f.id ?? ""} label={f.name ?? ""}>
                      <div>
                        <div>{f.name}</div>
                        <div class="text-xs text-muted-foreground">
                          DIA {f.defaultDia}h &middot; Peak {f.defaultPeak}min &middot; U-{f.concentration}
                        </div>
                      </div>
                    </Select.Item>
                  {/each}
                  <Select.Item value="" label="Custom">
                    <div>
                      <div>Custom</div>
                      <div class="text-xs text-muted-foreground">Enter a custom insulin name</div>
                    </div>
                  </Select.Item>
                </Select.Content>
              </Select.Root>
            </div>
          {/if}

          <div class="space-y-2">
            <Label for="insulin-name">Name / Brand</Label>
            <Input
              name="name"
              id="insulin-name"
              bind:value={insulinName}
              placeholder="e.g. Humalog, Tresiba, Fiasp"
              disabled={!!insulinFormulationId}
            />
          </div>

          <div class="space-y-2">
            <Label for="insulin-role">Role</Label>
            <Select.Root type="single" name="role" bind:value={insulinRole}>
              <Select.Trigger id="insulin-role">
                {insulinRoleLabels[insulinRole] ?? insulinRole}
              </Select.Trigger>
              <Select.Content>
                {#each Object.entries(insulinRoleLabels) as [value, label]}
                  <Select.Item {value} {label}>
                    <div>
                      <div>{label}</div>
                      <div class="text-xs text-muted-foreground">
                        {insulinRoleDescriptions[value] ?? ""}
                      </div>
                    </div>
                  </Select.Item>
                {/each}
              </Select.Content>
            </Select.Root>
          </div>

          <div class="grid gap-4 sm:grid-cols-3">
            <div class="space-y-2">
              <Label for="insulin-dia">DIA</Label>
              <div class="flex items-center gap-2">
                <Input
                  id="insulin-dia"
                  type="number"
                  bind:value={insulinDia}
                  step={0.5}
                  min={0.5}
                  max={48}
                  class="flex-1"
                />
                <span class="text-sm text-muted-foreground whitespace-nowrap">hours</span>
              </div>
            </div>

            <div class="space-y-2">
              <Label for="insulin-peak">Peak</Label>
              <div class="flex items-center gap-2">
                <Input
                  id="insulin-peak"
                  type="number"
                  bind:value={insulinPeak}
                  step={5}
                  min={0}
                  class="flex-1"
                />
                <span class="text-sm text-muted-foreground whitespace-nowrap">min</span>
              </div>
            </div>

            <div class="space-y-2">
              <Label for="insulin-concentration">Concentration</Label>
              <div class="flex items-center gap-2">
                <span class="text-sm text-muted-foreground whitespace-nowrap">U-</span>
                <Input
                  id="insulin-concentration"
                  type="number"
                  bind:value={insulinConcentration}
                  step={100}
                  min={100}
                  class="flex-1"
                />
              </div>
            </div>
          </div>

          <div class="grid gap-4 sm:grid-cols-2">
            <div class="space-y-2">
              <Label for="insulin-start">Start Date</Label>
              <Input
                name="startDate"
                id="insulin-start"
                type="date"
                bind:value={insulinStartDate}
              />
            </div>
            <div class="space-y-2">
              <Label for="insulin-end">End Date</Label>
              <Input
                name="endDate"
                id="insulin-end"
                type="date"
                bind:value={insulinEndDate}
              />
            </div>
          </div>

          <div class="flex items-center gap-4">
            <div class="flex items-center gap-2">
              <input
                id="insulin-current"
                type="checkbox"
                name="isCurrent"
                bind:checked={insulinIsCurrent}
                class="h-4 w-4 rounded border-input"
              />
              <Label for="insulin-current">Currently in use</Label>
            </div>

            <div class="flex items-center gap-2">
              <input
                id="insulin-primary"
                type="checkbox"
                name="isPrimary"
                bind:checked={insulinIsPrimary}
                class="h-4 w-4 rounded border-input"
              />
              <Label for="insulin-primary">Primary for this role</Label>
            </div>
          </div>

          <div class="space-y-2">
            <Label for="insulin-notes">Notes</Label>
            <Textarea
              name="notes"
              id="insulin-notes"
              bind:value={insulinNotes}
              placeholder="Any additional notes about this insulin"
              rows={2}
            />
          </div>

          <!-- Hidden fields for form submission -->
          <input type="hidden" name="n:dia" value={insulinDia} />
          <input type="hidden" name="n:peak" value={insulinPeak} />
          <input type="hidden" name="curve" value={insulinCurve} />
          <input type="hidden" name="n:concentration" value={insulinConcentration} />
          {#if insulinFormulationId}
            <input type="hidden" name="formulationId" value={insulinFormulationId} />
          {/if}
        </div>

        <Dialog.Footer>
          <Button
            type="button"
            variant="outline"
            onclick={() => (dialogOpen = false)}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={dialogSaving}>
            {#if dialogSaving}
              <Loader2 class="mr-2 h-4 w-4 animate-spin" />
            {:else}
              <Save class="mr-2 h-4 w-4" />
            {/if}
            {editing ? "Update" : "Add"} Insulin
          </Button>
        </Dialog.Footer>
      </form>
    </Dialog.Content>
  </Dialog.Root>

  <!-- Insulin Delete Confirmation -->
  <AlertDialog.Root
    open={deleteId !== null}
    onOpenChange={(open) => {
      if (!open) deleteId = null;
    }}
  >
    <AlertDialog.Content>
      <AlertDialog.Header>
        <AlertDialog.Title>Delete Insulin</AlertDialog.Title>
        <AlertDialog.Description>
          Are you sure you want to delete this insulin? This action cannot be
          undone.
        </AlertDialog.Description>
      </AlertDialog.Header>
      <AlertDialog.Footer>
        <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
        <AlertDialog.Action
          class="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          onclick={handleDelete}
        >
          Delete
        </AlertDialog.Action>
      </AlertDialog.Footer>
    </AlertDialog.Content>
  </AlertDialog.Root>
{/if}
