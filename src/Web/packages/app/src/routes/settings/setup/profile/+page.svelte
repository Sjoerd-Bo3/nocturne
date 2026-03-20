<script lang="ts">
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import ScheduleEditor from "$lib/components/setup/ScheduleEditor.svelte";
  import TargetRangeEditor from "$lib/components/setup/TargetRangeEditor.svelte";
  import * as Card from "$lib/components/ui/card";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Select from "$lib/components/ui/select";
  import { CheckCircle } from "lucide-svelte";
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

  // ── Form state ────────────────────────────────────────────────────

  let saving = $state(false);
  let saveError = $state<string | null>(null);

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

  // ── Save handler ────────────────────────────────────────────────

  async function handleSave(): Promise<boolean> {
    saving = true;
    saveError = null;
    try {
      const timestamp = new Date().toISOString();

      // 1. Therapy settings
      const settingsPayload = {
        profileName,
        units,
        timezone,
        dia,
        carbsHr,
        timestamp,
      };
      if (existingSettingsId) {
        await updateTherapySettings({
          id: existingSettingsId,
          request: settingsPayload,
        });
      } else {
        await createTherapySettings(settingsPayload);
      }

      // 2. Basal schedule
      const basalPayload = {
        profileName,
        entries: basalEntries.map((e) => ({ time: e.time, value: e.value })),
        timestamp,
      };
      if (existingBasalId) {
        await updateBasalSchedule({
          id: existingBasalId,
          request: basalPayload,
        });
      } else {
        await createBasalSchedule(basalPayload);
      }

      // 3. Carb ratio schedule
      const carbRatioPayload = {
        profileName,
        entries: carbRatioEntries.map((e) => ({
          time: e.time,
          value: e.value,
        })),
        timestamp,
      };
      if (existingCarbRatioId) {
        await updateCarbRatioSchedule({
          id: existingCarbRatioId,
          request: carbRatioPayload,
        });
      } else {
        await createCarbRatioSchedule(carbRatioPayload);
      }

      // 4. Sensitivity schedule
      const sensitivityPayload = {
        profileName,
        entries: sensitivityEntries.map((e) => ({
          time: e.time,
          value: e.value,
        })),
        timestamp,
      };
      if (existingSensitivityId) {
        await updateSensitivitySchedule({
          id: existingSensitivityId,
          request: sensitivityPayload,
        });
      } else {
        await createSensitivitySchedule(sensitivityPayload);
      }

      // 5. Target range schedule
      const targetRangePayload = {
        profileName,
        entries: targetEntries.map((e) => ({
          time: e.time,
          low: e.low,
          high: e.high,
        })),
        timestamp,
      };
      if (existingTargetRangeId) {
        await updateTargetRangeSchedule({
          id: existingTargetRangeId,
          request: targetRangePayload,
        });
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

<WizardShell
  title="Therapy Profile"
  description="Configure your therapy profile with basal rates, carb ratios, sensitivity factors, and target ranges."
  currentStep={5}
  totalSteps={5}
  prevHref="/settings/setup/connectors"
  nextHref="/settings/setup"
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
              <span class="text-sm text-muted-foreground whitespace-nowrap"
                >hours</span
              >
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
              <span class="text-sm text-muted-foreground whitespace-nowrap"
                >g/hr</span
              >
            </div>
          </div>
        </div>
      </Card.Content>
    </Card.Root>

    <!-- Basal Rates -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Basal Rates</Card.Title>
      </Card.Header>
      <Card.Content>
        <ScheduleEditor
          label="Basal Rates"
          unit="U/hr"
          bind:entries={basalEntries}
          step={0.05}
        />
      </Card.Content>
    </Card.Root>

    <!-- Carb Ratios -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Carb Ratios</Card.Title>
      </Card.Header>
      <Card.Content>
        <ScheduleEditor
          label="Carb Ratios (I:C)"
          unit="g/U"
          bind:entries={carbRatioEntries}
          step={0.5}
        />
      </Card.Content>
    </Card.Root>

    <!-- Insulin Sensitivity -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Insulin Sensitivity</Card.Title>
      </Card.Header>
      <Card.Content>
        <ScheduleEditor
          label="Insulin Sensitivity (ISF)"
          unit={sensitivityUnit}
          bind:entries={sensitivityEntries}
          step={1}
        />
      </Card.Content>
    </Card.Root>

    <!-- Target Range -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Target Range</Card.Title>
      </Card.Header>
      <Card.Content>
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
