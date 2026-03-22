<script lang="ts">
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Select from "$lib/components/ui/select";
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import * as patientRemote from "$api/generated/patientRecords.generated.remote";
  import { DiabetesType } from "$api";

  // ── Data loading ──────────────────────────────────────────────────

  const record = patientRemote.getPatientRecord();

  // ── Label map ─────────────────────────────────────────────────────

  const diabetesTypeLabels: Record<string, string> = {
    [DiabetesType.Type1]: "Type 1",
    [DiabetesType.Type2]: "Type 2",
    [DiabetesType.LADA]: "LADA",
    [DiabetesType.MODY]: "MODY",
    [DiabetesType.Gestational]: "Gestational",
    [DiabetesType.Other]: "Other",
  };

  // ── Form state ────────────────────────────────────────────────────

  let saving = $state(false);
  let saveError = $state<string | null>(null);
  let diabetesType = $state<string>("");
  let preferredName = $state("");
  let dateOfBirth = $state("");
  let diagnosisDate = $state("");

  // ── Pre-populate from existing record ─────────────────────────────

  $effect(() => {
    const r = record.current;
    if (r) {
      diabetesType = r.diabetesType ?? "";
      preferredName = r.preferredName ?? "";
      dateOfBirth = r.dateOfBirth
        ? new Date(r.dateOfBirth).toISOString().split("T")[0]
        : "";
      diagnosisDate = r.diagnosisDate
        ? new Date(r.diagnosisDate).toISOString().split("T")[0]
        : "";
    }
  });

  // ── Save handler ──────────────────────────────────────────────────

  async function handleSave(): Promise<boolean> {
    saving = true;
    saveError = null;
    try {
      await patientRemote.updatePatientRecord({
        ...record.current,
        diabetesType: (diabetesType as DiabetesType) || undefined,
        preferredName: preferredName || undefined,
        dateOfBirth: dateOfBirth || undefined,
        diagnosisDate: diagnosisDate || undefined,
      });
      return true;
    } catch {
      saveError = "Something went wrong. Please try again.";
      return false;
    } finally {
      saving = false;
    }
  }
</script>

<svelte:head>
  <title>Patient Record - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Patient Record"
  description="Tell us a bit about yourself and your diabetes. Only diabetes type is required."
  currentStep={1}
  totalSteps={5}
  nextHref="/settings/setup/devices"
  saveDisabled={!diabetesType}
  {saving}
  onSave={handleSave}
>
  <div class="grid gap-4 sm:grid-cols-2">
    <div class="space-y-2">
      <Label for="diabetes-type">Diabetes Type</Label>
      <Select.Root type="single" bind:value={diabetesType}>
        <Select.Trigger id="diabetes-type">
          {diabetesType
            ? (diabetesTypeLabels[diabetesType] ?? diabetesType)
            : "Select type"}
        </Select.Trigger>
        <Select.Content>
          {#each Object.entries(diabetesTypeLabels) as [value, label]}
            <Select.Item {value} {label} />
          {/each}
        </Select.Content>
      </Select.Root>
    </div>

    <div class="space-y-2">
      <Label for="preferred-name">Preferred Name</Label>
      <Input
        id="preferred-name"
        bind:value={preferredName}
        placeholder="How you'd like to be addressed"
      />
    </div>

    <div class="space-y-2">
      <Label for="date-of-birth">Date of Birth</Label>
      <Input
        id="date-of-birth"
        type="date"
        bind:value={dateOfBirth}
      />
    </div>

    <div class="space-y-2">
      <Label for="diagnosis-date">Diagnosis Date</Label>
      <Input
        id="diagnosis-date"
        type="date"
        bind:value={diagnosisDate}
      />
    </div>
  </div>

  {#if saveError}
    <p class="text-sm text-destructive">{saveError}</p>
  {/if}
</WizardShell>
