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
    InsulinCategory,
  } from "$api";
  import {
    insulinCategoryLabels,
    insulinCategoryDescriptions,
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

  // ── Inline variant state ────────────────────────────────────────

  let showInlineForm = $state(false);
  let inlineCategory = $state("");
  let inlineName = $state("");

  let canAddInline = $derived(inlineCategory !== "" && inlineName.trim() !== "");

  function resetInlineForm() {
    inlineCategory = "";
    inlineName = "";
    showInlineForm = false;
  }

  // ── Dialog variant state ────────────────────────────────────────

  let dialogOpen = $state(false);
  let editing = $state<PatientInsulin | null>(null);
  let deleteId = $state<string | null>(null);

  let insulinCategory = $state<string>(InsulinCategory.RapidActing);
  let insulinName = $state("");
  let insulinStartDate = $state("");
  let insulinEndDate = $state("");
  let insulinIsCurrent = $state(true);
  let insulinNotes = $state("");

  const activeForm = $derived(editing?.id ? insulinList.updateForm : insulinList.createForm);
  const dialogSaving = $derived(!!insulinList.createForm.pending || !!insulinList.updateForm.pending);

  function openDialog(insulin?: PatientInsulin) {
    if (insulin) {
      editing = insulin;
      insulinCategory = insulin.insulinCategory ?? InsulinCategory.RapidActing;
      insulinName = insulin.name ?? "";
      insulinStartDate = insulin.startDate
        ? new Date(insulin.startDate).toISOString().split("T")[0]
        : "";
      insulinEndDate = insulin.endDate
        ? new Date(insulin.endDate).toISOString().split("T")[0]
        : "";
      insulinIsCurrent = insulin.isCurrent ?? true;
      insulinNotes = insulin.notes ?? "";
    } else {
      editing = null;
      insulinCategory = InsulinCategory.RapidActing;
      insulinName = "";
      insulinStartDate = "";
      insulinEndDate = "";
      insulinIsCurrent = true;
      insulinNotes = "";
    }
    dialogOpen = true;
  }

  async function handleDelete() {
    if (!deleteId) return;
    await insulinList.remove(deleteId);
    deleteId = null;
  }

  // ── Inline category items with descriptions ─────────────────────

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
                {#if insulin.insulinCategory && insulinCategoryDescriptions[insulin.insulinCategory]}
                  &middot; {insulinCategoryDescriptions[insulin.insulinCategory]}
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
              <Select.Root type="single" name="insulinCategory" bind:value={inlineCategory}>
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

            <div class="space-y-2">
              <Label for="insulin-name">Brand / Name</Label>
              <Input
                name="name"
                id="insulin-name"
                bind:value={inlineName}
                placeholder="e.g. Humalog, Lantus"
              />
            </div>
          </div>
          <input type="hidden" name="b:isCurrent" value="on" />
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
              {#if insulin.isCurrent}
                <Badge
                  variant="default"
                  class="text-xs bg-green-600 hover:bg-green-700"
                >
                  Current
                </Badge>
              {/if}
            </div>
            <div class="flex items-center gap-3 text-xs text-muted-foreground flex-wrap">
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
    <Dialog.Content class="sm:max-w-lg">
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
            <Select.Root type="single" name="insulinCategory" bind:value={insulinCategory}>
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

          <div class="space-y-2">
            <Label for="insulin-name">Name / Brand</Label>
            <Input
              name="name"
              id="insulin-name"
              bind:value={insulinName}
              placeholder="e.g. Humalog, Tresiba, Fiasp"
            />
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
