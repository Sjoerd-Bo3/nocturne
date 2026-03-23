<script lang="ts">
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button";
  import { ChevronLeft, ChevronRight, SkipForward } from "lucide-svelte";

  interface Props {
    title: string;
    description: string;
    currentStep: number;
    totalSteps: number;
    /** Route for the previous step. If undefined, Back is hidden. */
    prevHref?: string;
    /** Route for the next step. */
    nextHref: string;
    /** Whether to show the Skip button. */
    showSkip?: boolean;
    /** Whether the Save & Continue button is disabled (e.g. validation errors). */
    saveDisabled?: boolean;
    /** Whether a save is in progress. */
    saving?: boolean;
    /** Called when Save & Continue is clicked. Must return true to proceed. */
    onSave?: () => Promise<boolean>;
    /** HTML form id — when set, the Save button submits this form instead of calling onSave. */
    formId?: string;
    children: import("svelte").Snippet;
  }

  let {
    title,
    description,
    currentStep,
    totalSteps,
    prevHref,
    nextHref,
    showSkip = true,
    saveDisabled = false,
    saving = false,
    onSave,
    formId,
    children,
  }: Props = $props();

  const isLastStep = $derived(currentStep === totalSteps);

  async function handleSave() {
    if (!onSave) return;
    const success = await onSave();
    if (success) {
      goto(nextHref);
    }
  }

  function handleSkip() {
    goto(nextHref);
  }
</script>

<div class="container mx-auto p-6 max-w-3xl space-y-6">
  <div class="space-y-1">
    <p class="text-sm text-muted-foreground">
      Step {currentStep} of {totalSteps}
    </p>
    <h1 class="text-2xl font-bold tracking-tight">{title}</h1>
    <p class="text-muted-foreground">{description}</p>
  </div>

  <div class="space-y-6">
    {@render children()}
  </div>

  <div class="flex items-center justify-between pt-4 border-t">
    <div>
      {#if prevHref}
        <Button variant="ghost" href={prevHref}>
          <ChevronLeft class="h-4 w-4 mr-1" />
          Back
        </Button>
      {/if}
    </div>
    <div class="flex gap-2">
      {#if showSkip}
        <Button variant="outline" onclick={handleSkip}>
          Skip
          <SkipForward class="h-4 w-4 ml-1" />
        </Button>
      {/if}
      <Button
        type={formId ? "submit" : "button"}
        form={formId}
        onclick={formId ? undefined : handleSave}
        disabled={saveDisabled || saving}
      >
        {#if saving}
          Saving...
        {:else if isLastStep}
          Finish
        {:else}
          Save & Continue
          <ChevronRight class="h-4 w-4 ml-1" />
        {/if}
      </Button>
    </div>
  </div>
</div>
