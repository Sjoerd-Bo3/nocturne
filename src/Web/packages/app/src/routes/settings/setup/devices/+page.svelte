<script lang="ts">
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import { PatientDeviceManager } from "$lib/components/patient";

  async function handleSave(): Promise<boolean> {
    return true;
  }
</script>

<svelte:head>
  <title>Devices - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Your Devices"
  description="Add the diabetes devices you use. You can always update these later."
  currentStep={3}
  totalSteps={7}
  prevHref="/settings/setup/patient"
  nextHref="/settings/setup/insulins"
  showSkip={true}
  saveDisabled={false}
  onSave={handleSave}
>
  <PatientDeviceManager variant="inline" />
</WizardShell>
