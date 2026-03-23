<script lang="ts">
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Select from "$lib/components/ui/select";
  import { DiabetesType } from "$api";
  import { diabetesTypeLabels } from "./labels";
  import { createClinicalState } from "./state.svelte";

  interface Props {
    /** Show extended fields (pronouns, diabetesTypeOther). Default: true */
    extended?: boolean;
    /** Expose reactive state API to parent */
    onstate?: (api: {
      save: () => Promise<boolean>;
      saving: boolean;
      isValid: boolean;
    }) => void;
  }

  let { extended = true, onstate }: Props = $props();

  const state = createClinicalState();

  // Expose state API to parent reactively
  $effect(() => {
    onstate?.({
      save: state.save,
      saving: state.saving,
      isValid: state.isValid,
    });
  });
</script>

<div class="grid gap-4 sm:grid-cols-2">
  <div class="space-y-2">
    <Label for="diabetes-type">Diabetes Type</Label>
    <Select.Root type="single" bind:value={state.diabetesType}>
      <Select.Trigger id="diabetes-type">
        {state.diabetesType
          ? (diabetesTypeLabels[state.diabetesType] ?? state.diabetesType)
          : "Select type"}
      </Select.Trigger>
      <Select.Content>
        {#each Object.entries(diabetesTypeLabels) as [value, label]}
          <Select.Item {value} {label} />
        {/each}
      </Select.Content>
    </Select.Root>
  </div>

  {#if extended && state.diabetesType === DiabetesType.Other}
    <div class="space-y-2">
      <Label for="diabetes-type-other">Specify Type</Label>
      <Input
        id="diabetes-type-other"
        bind:value={state.diabetesTypeOther}
        placeholder="e.g. Type 3c"
      />
    </div>
  {/if}

  <div class="space-y-2">
    <Label for="diagnosis-date">Diagnosis Date</Label>
    <Input
      id="diagnosis-date"
      type="date"
      bind:value={state.diagnosisDate}
    />
  </div>

  <div class="space-y-2">
    <Label for="date-of-birth">Date of Birth</Label>
    <Input
      id="date-of-birth"
      type="date"
      bind:value={state.dateOfBirth}
    />
  </div>

  <div class="space-y-2">
    <Label for="preferred-name">Preferred Name</Label>
    <Input
      id="preferred-name"
      bind:value={state.preferredName}
      placeholder="How you'd like to be addressed"
    />
  </div>

  {#if extended}
    <div class="space-y-2">
      <Label for="pronouns">Pronouns</Label>
      <Input
        id="pronouns"
        bind:value={state.pronouns}
        placeholder="e.g. she/her, he/him, they/them"
      />
    </div>
  {/if}
</div>

{#if state.saveError}
  <p class="text-sm text-destructive">{state.saveError}</p>
{/if}
