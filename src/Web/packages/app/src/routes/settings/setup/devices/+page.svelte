<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Card from "$lib/components/ui/card";
  import * as Select from "$lib/components/ui/select";
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import * as patientRemote from "$api/generated/patientRecords.generated.remote";
  import { DeviceCategory, AidAlgorithm } from "$api";
  import {
    Plus,
    Trash2,
    Cpu,
    Activity,
    Droplets,
    Syringe,
    PenLine,
    Upload,
  } from "lucide-svelte";

  // ── Data loading ──────────────────────────────────────────────────
  // createDevice and deleteDevice call getDevices().refresh() internally,
  // so this reactive query updates automatically after mutations.
  const devices = patientRemote.getDevices();

  // ── Label maps ────────────────────────────────────────────────────

  const categoryLabels: Record<string, string> = {
    [DeviceCategory.InsulinPump]: "Insulin Pump",
    [DeviceCategory.CGM]: "CGM",
    [DeviceCategory.GlucoseMeter]: "Glucose Meter",
    [DeviceCategory.InsulinPen]: "Insulin Pen",
    [DeviceCategory.SmartPen]: "Smart Pen",
    [DeviceCategory.Uploader]: "Uploader",
  };

  const aidAlgorithmLabels: Record<string, string> = {
    [AidAlgorithm.None]: "None",
    [AidAlgorithm.OpenAps]: "OpenAPS",
    [AidAlgorithm.AndroidAps]: "AndroidAPS",
    [AidAlgorithm.Loop]: "Loop",
    [AidAlgorithm.Trio]: "Trio",
    [AidAlgorithm.IAPS]: "iAPS",
    [AidAlgorithm.ControlIQ]: "Control-IQ",
    [AidAlgorithm.CamAPSFX]: "CamAPS FX",
    [AidAlgorithm.Omnipod5Algorithm]: "Omnipod 5",
    [AidAlgorithm.MedtronicSmartGuard]: "Medtronic SmartGuard",
  };

  const categoryIcons: Record<string, typeof Cpu> = {
    [DeviceCategory.InsulinPump]: Cpu,
    [DeviceCategory.CGM]: Activity,
    [DeviceCategory.GlucoseMeter]: Droplets,
    [DeviceCategory.InsulinPen]: Syringe,
    [DeviceCategory.SmartPen]: PenLine,
    [DeviceCategory.Uploader]: Upload,
  };

  // ── Form state ────────────────────────────────────────────────────

  let showForm = $state(false);
  let deviceCategory = $state("");
  let manufacturer = $state("");
  let model = $state("");
  let aidAlgorithm = $state("");

  let showAidField = $derived(deviceCategory === DeviceCategory.InsulinPump);

  let canAdd = $derived(
    deviceCategory !== "" && manufacturer.trim() !== "" && model.trim() !== "",
  );

  const createForm = patientRemote.createDevice;

  // ── Handlers ──────────────────────────────────────────────────────

  function resetForm() {
    deviceCategory = "";
    manufacturer = "";
    model = "";
    aidAlgorithm = "";
    showForm = false;
  }

  async function handleDelete(id: string) {
    await patientRemote.deleteDevice(id);
  }

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
  currentStep={2}
  totalSteps={6}
  prevHref="/settings/setup/patient"
  nextHref="/settings/setup/insulins"
  showSkip={true}
  saveDisabled={false}
  onSave={handleSave}
>
  <!-- Existing device cards -->
  {#if devices.current && devices.current.length > 0}
    <div class="space-y-3">
      {#each devices.current as device}
        <Card.Root>
          <Card.Header class="flex flex-row items-center gap-3 py-3">
            <div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-muted">
              {#if device.deviceCategory && categoryIcons[device.deviceCategory]}
                {@const Icon = categoryIcons[device.deviceCategory]}
                <Icon class="h-4 w-4" />
              {:else}
                <Cpu class="h-4 w-4" />
              {/if}
            </div>
            <div class="flex-1 min-w-0">
              <Card.Title class="text-sm font-medium">
                {[device.manufacturer, device.model].filter(Boolean).join(" ") || "Unknown Device"}
              </Card.Title>
              <Card.Description class="text-xs">
                {device.deviceCategory
                  ? (categoryLabels[device.deviceCategory] ?? device.deviceCategory)
                  : "Unknown Category"}
                {#if device.aidAlgorithm && device.aidAlgorithm !== AidAlgorithm.None}
                  &middot; {aidAlgorithmLabels[device.aidAlgorithm] ?? device.aidAlgorithm}
                {/if}
              </Card.Description>
            </div>
            {#if device.id}
              <Button
                variant="ghost"
                size="icon"
                class="shrink-0 text-muted-foreground hover:text-destructive"
                onclick={() => handleDelete(device.id!)}
              >
                <Trash2 class="h-4 w-4" />
              </Button>
            {/if}
          </Card.Header>
        </Card.Root>
      {/each}
    </div>
  {/if}

  <!-- Add device button / inline form -->
  {#if showForm}
    <Card.Root>
      <Card.Header>
        <Card.Title class="text-sm">Add a Device</Card.Title>
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
              <Label for="device-category">Device Category</Label>
              <Select.Root type="single" name="deviceCategory" bind:value={deviceCategory}>
                <Select.Trigger id="device-category">
                  {deviceCategory
                    ? (categoryLabels[deviceCategory] ?? deviceCategory)
                    : "Select category"}
                </Select.Trigger>
                <Select.Content>
                  {#each Object.entries(categoryLabels) as [value, label]}
                    <Select.Item {value} {label} />
                  {/each}
                </Select.Content>
              </Select.Root>
            </div>

            <div class="space-y-2">
              <Label for="manufacturer">Manufacturer</Label>
              <Input
                name="manufacturer"
                id="manufacturer"
                bind:value={manufacturer}
                placeholder="e.g. Dexcom, Omnipod"
              />
            </div>

            <div class="space-y-2">
              <Label for="model">Model</Label>
              <Input
                name="model"
                id="model"
                bind:value={model}
                placeholder="e.g. G7, DASH"
              />
            </div>

            {#if showAidField}
              <div class="space-y-2">
                <Label for="aid-algorithm">AID Algorithm</Label>
                <Select.Root type="single" name="aidAlgorithm" bind:value={aidAlgorithm}>
                  <Select.Trigger id="aid-algorithm">
                    {aidAlgorithm
                      ? (aidAlgorithmLabels[aidAlgorithm] ?? aidAlgorithm)
                      : "Select algorithm"}
                  </Select.Trigger>
                  <Select.Content>
                    {#each Object.entries(aidAlgorithmLabels) as [value, label]}
                      <Select.Item {value} {label} />
                    {/each}
                  </Select.Content>
                </Select.Root>
              </div>
            {/if}
          </div>
          <input type="hidden" name="b:isCurrent" value="on" />
          {#each createForm.fields.allIssues() as issue}
            <p class="text-sm text-destructive">{issue.message}</p>
          {/each}
        </Card.Content>
        <Card.Footer class="flex justify-end gap-2">
          <Button type="button" variant="outline" onclick={resetForm} disabled={!!createForm.pending}>
            Cancel
          </Button>
          <Button type="submit" disabled={!canAdd || !!createForm.pending}>
            {createForm.pending ? "Adding..." : "Add Device"}
          </Button>
        </Card.Footer>
      </form>
    </Card.Root>
  {:else}
    <Button variant="outline" onclick={() => (showForm = true)}>
      <Plus class="h-4 w-4 mr-1" />
      Add Device
    </Button>
  {/if}
</WizardShell>
