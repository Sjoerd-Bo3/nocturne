<script lang="ts">
  import { tick } from "svelte";
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import ScheduleEditor from "$lib/components/setup/ScheduleEditor.svelte";
  import TargetRangeEditor from "$lib/components/setup/TargetRangeEditor.svelte";
  import * as Card from "$lib/components/ui/card";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import * as Select from "$lib/components/ui/select";
  import { CheckCircle } from "lucide-svelte";
  import { glucoseUnits } from "$lib/stores/appearance-store.svelte";
  import {
    getProfileSummary,
    createTherapySettings,
    updateTherapySettings,
    createBasalSchedule,
    updateBasalSchedule,
    createCarbRatioSchedule,
    updateCarbRatioSchedule,
    createSensitivitySchedule,
    updateSensitivitySchedule,
    createTargetRangeSchedule,
    updateTargetRangeSchedule,
  } from "$lib/api/generated/profiles.generated.remote";

  // ── Data loading ──────────────────────────────────────────────────

  const summaryQuery = getProfileSummary(undefined);

  // ── Form references ─────────────────────────────────────────────

  const createSettingsForm = createTherapySettings;
  const updateSettingsForm = updateTherapySettings;
  const createBasalForm = createBasalSchedule;
  const updateBasalForm = updateBasalSchedule;

  // Hidden form element refs for programmatic submission
  let settingsFormEl = $state<HTMLFormElement | null>(null);
  let basalFormEl = $state<HTMLFormElement | null>(null);

  // ── Form state ────────────────────────────────────────────────────

  let saveError = $state<string | null>(null);
  let saving = $state(false);

  // Basics
  let profileName = $state("Default");
  let units = $state("mg/dL");
  let timezone = $state("");
  let dia = $state(3.0);
  let carbsHr = $state(20);

  // Schedule entries
  let basalEntries = $state<Array<{ time: string; value: number }>>([
    { time: "00:00", value: 0.5 },
  ]);
  let carbRatioEntries = $state<Array<{ time: string; value: number }>>([
    { time: "00:00", value: 10 },
  ]);
  let sensitivityEntries = $state<Array<{ time: string; value: number }>>([
    { time: "00:00", value: 50 },
  ]);
  let targetEntries = $state<Array<{ time: string; low: number; high: number }>>(
    [{ time: "00:00", low: 70, high: 180 }],
  );

  // Track existing IDs for update vs create
  let existingSettingsId = $state<string | undefined>();
  let existingBasalId = $state<string | undefined>();
  let existingCarbRatioId = $state<string | undefined>();
  let existingSensitivityId = $state<string | undefined>();
  let existingTargetRangeId = $state<string | undefined>();

  // ── Auto-complete detection ─────────────────────────────────────

  const isExternallyManaged = $derived.by(() => {
    const settings = (summaryQuery.current?.therapySettings as any[])?.[0];
    return settings?.isExternallyManaged === true;
  });

  // ── Pre-populate from summary ───────────────────────────────────

  $effect(() => {
    const summary = summaryQuery.current;
    if (!summary) return;

    const settings = (summary.therapySettings as any[])?.[0];
    if (settings) {
      existingSettingsId = settings.id;
      profileName = settings.profileName ?? "Default";
      units = settings.units ?? "mg/dL";
      timezone = settings.timezone ?? "";
      dia = settings.dia ?? 3.0;
      carbsHr = settings.carbsHr ?? 20;
    }

    const basal = (summary.basalSchedules as any[])?.[0];
    if (basal) {
      existingBasalId = basal.id;
      basalEntries = (basal.entries ?? []).map((e: any) => ({
        time: e.time ?? "00:00",
        value: e.value ?? 0,
      }));
    }

    const carbRatio = (summary.carbRatioSchedules as any[])?.[0];
    if (carbRatio) {
      existingCarbRatioId = carbRatio.id;
      carbRatioEntries = (carbRatio.entries ?? []).map((e: any) => ({
        time: e.time ?? "00:00",
        value: e.value ?? 0,
      }));
    }

    const sensitivity = (summary.sensitivitySchedules as any[])?.[0];
    if (sensitivity) {
      existingSensitivityId = sensitivity.id;
      sensitivityEntries = (sensitivity.entries ?? []).map((e: any) => ({
        time: e.time ?? "00:00",
        value: e.value ?? 0,
      }));
    }

    const targetRange = (summary.targetRangeSchedules as any[])?.[0];
    if (targetRange) {
      existingTargetRangeId = targetRange.id;
      targetEntries = (targetRange.entries ?? []).map((e: any) => ({
        time: e.time ?? "00:00",
        low: e.low ?? 0,
        high: e.high ?? 0,
      }));
    }
  });

  // ── Derived labels ──────────────────────────────────────────────

  const sensitivityUnit = $derived(
    units === "mmol/L" ? "mmol/L per U" : "mg/dL per U",
  );
  const targetUnit = $derived(units === "mmol/L" ? "mmol/L" : "mg/dL");

  // Sync appearance store when units change
  $effect(() => {
    glucoseUnits.current = units === "mmol/L" ? "mmol" : "mg/dl";
  });

  // ── Programmatic form submission helpers ─────────────────────────

  let settingsSubmitResolve: ((success: boolean) => void) | null = null;
  let basalSubmitResolve: ((success: boolean) => void) | null = null;

  function submitHiddenForm(
    el: HTMLFormElement | null,
    setResolver: (resolver: (success: boolean) => void) => void,
  ): Promise<boolean> {
    return new Promise((resolve) => {
      setResolver(resolve);
      tick().then(() => el?.requestSubmit());
    });
  }

  // ── Save all profile data ───────────────────────────────────────

  async function handleSave(): Promise<boolean> {
    saveError = null;
    saving = true;

    try {
      // 1. Save therapy settings (form-based)
      const settingsOk = await submitHiddenForm(
        settingsFormEl,
        (r) => (settingsSubmitResolve = r),
      );
      if (!settingsOk) {
        saveError = "Failed to save therapy settings.";
        return false;
      }

      // 2. Save basal schedule (form-based)
      const basalOk = await submitHiddenForm(
        basalFormEl,
        (r) => (basalSubmitResolve = r),
      );
      if (!basalOk) {
        saveError = "Failed to save basal schedule.";
        return false;
      }

      // 3. Save remaining schedules (command-based)
      const timestamp = new Date().toISOString();

      const carbRatioPayload = {
        profileName,
        entries: carbRatioEntries.map((e) => ({ time: e.time, value: e.value })),
        timestamp,
      };
      if (existingCarbRatioId) {
        await updateCarbRatioSchedule({ id: existingCarbRatioId, request: carbRatioPayload });
      } else {
        await createCarbRatioSchedule(carbRatioPayload);
      }

      const sensitivityPayload = {
        profileName,
        entries: sensitivityEntries.map((e) => ({ time: e.time, value: e.value })),
        timestamp,
      };
      if (existingSensitivityId) {
        await updateSensitivitySchedule({ id: existingSensitivityId, request: sensitivityPayload });
      } else {
        await createSensitivitySchedule(sensitivityPayload);
      }

      const targetRangePayload = {
        profileName,
        entries: targetEntries.map((e) => ({ time: e.time, low: e.low, high: e.high })),
        timestamp,
      };
      if (existingTargetRangeId) {
        await updateTargetRangeSchedule({ id: existingTargetRangeId, request: targetRangePayload });
      } else {
        await createTargetRangeSchedule(targetRangePayload);
      }

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
  <title>Therapy Profile - Setup - Nocturne</title>
</svelte:head>

<!-- Hidden therapy settings form -->
{#if existingSettingsId}
  <form
    bind:this={settingsFormEl}
    class="hidden"
    {...updateSettingsForm.for(existingSettingsId).enhance(async ({ submit }) => {
      await submit();
      const success = !!updateSettingsForm.for(existingSettingsId!).result;
      settingsSubmitResolve?.(success);
      settingsSubmitResolve = null;
    })}
  >
    <input type="hidden" name="id" value={existingSettingsId} />
    <input type="hidden" name="request.profileName" value={profileName} />
    <input type="hidden" name="request.units" value={units} />
    <input type="hidden" name="request.timezone" value={timezone} />
    <input type="hidden" name="n:request.dia" value={dia} />
    <input type="hidden" name="n:request.carbsHr" value={carbsHr} />
    <input type="hidden" name="request.timestamp" value={new Date().toISOString()} />
  </form>
{:else}
  <form
    bind:this={settingsFormEl}
    class="hidden"
    {...createSettingsForm.enhance(async ({ submit }) => {
      await submit();
      const success = !!createSettingsForm.result;
      settingsSubmitResolve?.(success);
      settingsSubmitResolve = null;
    })}
  >
    <input type="hidden" name="profileName" value={profileName} />
    <input type="hidden" name="units" value={units} />
    <input type="hidden" name="timezone" value={timezone} />
    <input type="hidden" name="n:dia" value={dia} />
    <input type="hidden" name="n:carbsHr" value={carbsHr} />
    <input type="hidden" name="timestamp" value={new Date().toISOString()} />
  </form>
{/if}

<!-- Hidden basal schedule form -->
{#if existingBasalId}
  <form
    bind:this={basalFormEl}
    class="hidden"
    {...updateBasalForm.for(existingBasalId).enhance(async ({ submit }) => {
      await submit();
      const success = !!updateBasalForm.for(existingBasalId!).result;
      basalSubmitResolve?.(success);
      basalSubmitResolve = null;
    })}
  >
    <input type="hidden" name="id" value={existingBasalId} />
    <input type="hidden" name="request.profileName" value={profileName} />
    <input type="hidden" name="request.timestamp" value={new Date().toISOString()} />
    {#each basalEntries as entry, i}
      <input type="hidden" name="request.entries[{i}].time" value={entry.time} />
      <input type="hidden" name="n:request.entries[{i}].value" value={entry.value} />
    {/each}
  </form>
{:else}
  <form
    bind:this={basalFormEl}
    class="hidden"
    {...createBasalForm.enhance(async ({ submit }) => {
      await submit();
      const success = !!createBasalForm.result;
      basalSubmitResolve?.(success);
      basalSubmitResolve = null;
    })}
  >
    <input type="hidden" name="profileName" value={profileName} />
    <input type="hidden" name="timestamp" value={new Date().toISOString()} />
    {#each basalEntries as entry, i}
      <input type="hidden" name="entries[{i}].time" value={entry.time} />
      <input type="hidden" name="n:entries[{i}].value" value={entry.value} />
    {/each}
  </form>
{/if}

<WizardShell
  title="Therapy Profile"
  description="Configure your therapy profile with basal rates, carb ratios, sensitivity factors, and target ranges."
  currentStep={7}
  totalSteps={7}
  prevHref="/settings/setup/connectors"
  nextHref="/settings"
  showSkip={true}
  saveDisabled={!profileName}
  {saving}
  onSave={handleSave}
>
  {#if isExternallyManaged}
    <Card.Root>
      <Card.Content class="py-8 text-center space-y-2">
        <CheckCircle class="h-12 w-12 mx-auto text-green-500" />
        <p class="font-medium">Profile Synced Automatically</p>
        <p class="text-sm text-muted-foreground">
          Your therapy profile is being managed by a connected system. Changes
          will sync automatically.
        </p>
      </Card.Content>
    </Card.Root>
  {:else}
    <!-- Basics -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Basics</Card.Title>
      </Card.Header>
      <Card.Content>
        <div class="grid gap-4 sm:grid-cols-2">
          <div class="space-y-2">
            <Label for="profile-name">Profile Name</Label>
            <Input
              id="profile-name"
              bind:value={profileName}
              placeholder="Default"
            />
          </div>

          <div class="space-y-2">
            <Label for="units">Units</Label>
            <Select.Root type="single" bind:value={units}>
              <Select.Trigger id="units">
                {units || "Select units"}
              </Select.Trigger>
              <Select.Content>
                <Select.Item value="mg/dL" label="mg/dL" />
                <Select.Item value="mmol/L" label="mmol/L" />
              </Select.Content>
            </Select.Root>
          </div>

          <div class="space-y-2">
            <Label for="timezone">Timezone</Label>
            <Input
              id="timezone"
              bind:value={timezone}
              placeholder="America/New_York"
            />
          </div>

          <div class="space-y-2">
            <Label for="dia">Duration of Insulin Action</Label>
            <div class="flex items-center gap-2">
              <Input
                id="dia"
                type="number"
                bind:value={dia}
                step={0.5}
                min={1}
                class="flex-1"
              />
              <span class="text-sm text-muted-foreground whitespace-nowrap">hours</span>
            </div>
          </div>

          <div class="space-y-2">
            <Label for="carbs-hr">Carb Absorption Rate</Label>
            <div class="flex items-center gap-2">
              <Input
                id="carbs-hr"
                type="number"
                bind:value={carbsHr}
                step={1}
                min={1}
                class="flex-1"
              />
              <span class="text-sm text-muted-foreground whitespace-nowrap">g/hr</span>
            </div>
          </div>
        </div>
      </Card.Content>
    </Card.Root>

    <!-- Schedules -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Schedules</Card.Title>
      </Card.Header>
      <Card.Content class="space-y-6">
        <ScheduleEditor
          label="Basal Rates"
          unit="U/hr"
          bind:entries={basalEntries}
          step={0.05}
        />

        <Separator />

        <ScheduleEditor
          label="Carb Ratios (I:C)"
          unit="g/U"
          bind:entries={carbRatioEntries}
          step={0.5}
        />

        <Separator />

        <ScheduleEditor
          label="Insulin Sensitivity (ISF)"
          unit={sensitivityUnit}
          bind:entries={sensitivityEntries}
          step={1}
        />

        <Separator />

        <TargetRangeEditor
          label="Target Range"
          unit={targetUnit}
          bind:entries={targetEntries}
        />
      </Card.Content>
    </Card.Root>
  {/if}

  {#if saveError}
    <p class="text-sm text-destructive">{saveError}</p>
  {/if}
</WizardShell>
