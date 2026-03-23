<script lang="ts">
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import { PatientInsulinManager } from "$lib/components/patient";

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
  currentStep={4}
  totalSteps={7}
  prevHref="/settings/setup/devices"
  nextHref="/settings/setup/uploaders"
  showSkip={true}
  saveDisabled={false}
  onSave={handleSave}
>
  <PatientInsulinManager variant="inline" />
</WizardShell>
