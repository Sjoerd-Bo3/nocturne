<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Select from "$lib/components/ui/select";
  import * as Dialog from "$lib/components/ui/dialog";
  import * as AlertDialog from "$lib/components/ui/alert-dialog";
  import { Textarea } from "$lib/components/ui/textarea";
  import {
    Cpu,
    Syringe,
    Plus,
    Pencil,
    Trash2,
    Save,
    Loader2,
    HeartPulse,
  } from "lucide-svelte";
  import * as patientRemote from "$api/generated/patientRecords.generated.remote";
  import {
    type PatientDevice,
    type PatientInsulin,
    DiabetesType,
    DeviceCategory,
    AidAlgorithm,
    InsulinCategory,
  } from "$api";

  // ── Data loading ──────────────────────────────────────────────────

  const record = patientRemote.getPatientRecord();
  const devices = patientRemote.getDevices();
  const insulins = patientRemote.getInsulins();

  // ── Label maps ────────────────────────────────────────────────────

  const diabetesTypeLabels: Record<string, string> = {
    [DiabetesType.Type1]: "Type 1",
    [DiabetesType.Type2]: "Type 2",
    [DiabetesType.LADA]: "LADA",
    [DiabetesType.MODY]: "MODY",
    [DiabetesType.Gestational]: "Gestational",
    [DiabetesType.Other]: "Other",
  };

  const deviceCategoryLabels: Record<string, string> = {
    [DeviceCategory.InsulinPump]: "Insulin Pump",
    [DeviceCategory.CGM]: "CGM",
    [DeviceCategory.GlucoseMeter]: "Glucose Meter",
    [DeviceCategory.InsulinPen]: "Insulin Pen",
    [DeviceCategory.SmartPen]: "Smart Pen",
  };

  const aidAlgorithmLabels: Record<string, string> = {
    [AidAlgorithm.OpenAps]: "OpenAPS",
    [AidAlgorithm.AndroidAps]: "AndroidAPS",
    [AidAlgorithm.Loop]: "Loop",
    [AidAlgorithm.Trio]: "Trio",
    [AidAlgorithm.IAPS]: "iAPS",
    [AidAlgorithm.ControlIQ]: "Control-IQ",
    [AidAlgorithm.CamAPSFX]: "CamAPS FX",
    [AidAlgorithm.Omnipod5Algorithm]: "Omnipod 5",
    [AidAlgorithm.MedtronicSmartGuard]: "SmartGuard",
    [AidAlgorithm.None]: "None",
    [AidAlgorithm.Unknown]: "Unknown",
  };

  const insulinCategoryLabels: Record<string, string> = {
    [InsulinCategory.RapidActing]: "Rapid Acting",
    [InsulinCategory.ShortActing]: "Short Acting",
    [InsulinCategory.IntermediateActing]: "Intermediate Acting",
    [InsulinCategory.LongActing]: "Long Acting",
    [InsulinCategory.UltraLongActing]: "Ultra Long Acting",
    [InsulinCategory.Premixed]: "Premixed",
  };

  // ── Clinical Info form state ──────────────────────────────────────

  let clinicalSaving = $state(false);
  let clinicalDiabetesType = $state<string>("");
  let clinicalDiabetesTypeOther = $state("");
  let clinicalDiagnosisDate = $state("");
  let clinicalDateOfBirth = $state("");
  let clinicalPreferredName = $state("");
  let clinicalPronouns = $state("");

  // Sync form state when record loads
  $effect(() => {
    const r = record.current;
    if (r) {
      clinicalDiabetesType = r.diabetesType ?? "";
      clinicalDiabetesTypeOther = r.diabetesTypeOther ?? "";
      clinicalDiagnosisDate = r.diagnosisDate
        ? new Date(r.diagnosisDate).toISOString().split("T")[0]
        : "";
      clinicalDateOfBirth = r.dateOfBirth
        ? new Date(r.dateOfBirth).toISOString().split("T")[0]
        : "";
      clinicalPreferredName = r.preferredName ?? "";
      clinicalPronouns = r.pronouns ?? "";
    }
  });

  async function saveClinicalInfo() {
    clinicalSaving = true;
    try {
      await patientRemote.updatePatientRecord({
        ...record.current,
        diabetesType:
          (clinicalDiabetesType as DiabetesType) || undefined,
        diabetesTypeOther:
          clinicalDiabetesType === DiabetesType.Other
            ? clinicalDiabetesTypeOther
            : undefined,
        diagnosisDate: clinicalDiagnosisDate
          ? new Date(clinicalDiagnosisDate)
          : undefined,
        dateOfBirth: clinicalDateOfBirth
          ? new Date(clinicalDateOfBirth)
          : undefined,
        preferredName: clinicalPreferredName || undefined,
        pronouns: clinicalPronouns || undefined,
      });
    } finally {
      clinicalSaving = false;
    }
  }

  // ── Device dialog state ───────────────────────────────────────────

  let deviceDialogOpen = $state(false);
  let deviceEditing = $state<PatientDevice | null>(null);
  let deviceSaving = $state(false);
  let deviceDeleteId = $state<string | null>(null);

  let deviceCategory = $state<string>(DeviceCategory.InsulinPump);
  let deviceManufacturer = $state("");
  let deviceModel = $state("");
  let deviceAidAlgorithm = $state<string>("");
  let deviceSerialNumber = $state("");
  let deviceStartDate = $state("");
  let deviceEndDate = $state("");
  let deviceIsCurrent = $state(true);
  let deviceNotes = $state("");

  function openDeviceDialog(device?: PatientDevice) {
    if (device) {
      deviceEditing = device;
      deviceCategory = device.deviceCategory ?? DeviceCategory.InsulinPump;
      deviceManufacturer = device.manufacturer ?? "";
      deviceModel = device.model ?? "";
      deviceAidAlgorithm = device.aidAlgorithm ?? "";
      deviceSerialNumber = device.serialNumber ?? "";
      deviceStartDate = device.startDate
        ? new Date(device.startDate).toISOString().split("T")[0]
        : "";
      deviceEndDate = device.endDate
        ? new Date(device.endDate).toISOString().split("T")[0]
        : "";
      deviceIsCurrent = device.isCurrent ?? true;
      deviceNotes = device.notes ?? "";
    } else {
      deviceEditing = null;
      deviceCategory = DeviceCategory.InsulinPump;
      deviceManufacturer = "";
      deviceModel = "";
      deviceAidAlgorithm = "";
      deviceSerialNumber = "";
      deviceStartDate = "";
      deviceEndDate = "";
      deviceIsCurrent = true;
      deviceNotes = "";
    }
    deviceDialogOpen = true;
  }

  async function saveDevice() {
    deviceSaving = true;
    try {
      const payload: PatientDevice = {
        deviceCategory: deviceCategory as DeviceCategory,
        manufacturer: deviceManufacturer || undefined,
        model: deviceModel || undefined,
        aidAlgorithm:
          deviceCategory === DeviceCategory.InsulinPump && deviceAidAlgorithm
            ? (deviceAidAlgorithm as AidAlgorithm)
            : undefined,
        serialNumber: deviceSerialNumber || undefined,
        startDate: deviceStartDate ? new Date(deviceStartDate) : undefined,
        endDate: deviceEndDate ? new Date(deviceEndDate) : undefined,
        isCurrent: deviceIsCurrent,
        notes: deviceNotes || undefined,
      };

      if (deviceEditing?.id) {
        await patientRemote.updateDevice({
          id: deviceEditing.id,
          request: payload,
        });
      } else {
        await patientRemote.createDevice(payload);
      }
      deviceDialogOpen = false;
    } finally {
      deviceSaving = false;
    }
  }

  async function deleteDevice() {
    if (!deviceDeleteId) return;
    await patientRemote.deleteDevice(deviceDeleteId);
    deviceDeleteId = null;
  }

  // ── Insulin dialog state ──────────────────────────────────────────

  let insulinDialogOpen = $state(false);
  let insulinEditing = $state<PatientInsulin | null>(null);
  let insulinSaving = $state(false);
  let insulinDeleteId = $state<string | null>(null);

  let insulinCategory = $state<string>(InsulinCategory.RapidActing);
  let insulinName = $state("");
  let insulinStartDate = $state("");
  let insulinEndDate = $state("");
  let insulinIsCurrent = $state(true);
  let insulinNotes = $state("");

  function openInsulinDialog(insulin?: PatientInsulin) {
    if (insulin) {
      insulinEditing = insulin;
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
      insulinEditing = null;
      insulinCategory = InsulinCategory.RapidActing;
      insulinName = "";
      insulinStartDate = "";
      insulinEndDate = "";
      insulinIsCurrent = true;
      insulinNotes = "";
    }
    insulinDialogOpen = true;
  }

  async function saveInsulin() {
    insulinSaving = true;
    try {
      const payload: PatientInsulin = {
        insulinCategory: insulinCategory as InsulinCategory,
        name: insulinName || undefined,
        startDate: insulinStartDate ? new Date(insulinStartDate) : undefined,
        endDate: insulinEndDate ? new Date(insulinEndDate) : undefined,
        isCurrent: insulinIsCurrent,
        notes: insulinNotes || undefined,
      };

      if (insulinEditing?.id) {
        await patientRemote.updateInsulin({
          id: insulinEditing.id,
          request: payload,
        });
      } else {
        await patientRemote.createInsulin(payload);
      }
      insulinDialogOpen = false;
    } finally {
      insulinSaving = false;
    }
  }

  async function deleteInsulin() {
    if (!insulinDeleteId) return;
    await patientRemote.deleteInsulin(insulinDeleteId);
    insulinDeleteId = null;
  }

  // ── Helpers ───────────────────────────────────────────────────────

  function formatDate(date: Date | string | undefined): string {
    if (!date) return "";
    const d = new Date(date);
    return d.toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  }

  const showAidAlgorithm = $derived(
    deviceCategory === DeviceCategory.InsulinPump,
  );
</script>

<svelte:head>
  <title>Patient Record - Settings - Nocturne</title>
</svelte:head>

<div class="w-full py-6 space-y-6">
  <div class="space-y-1">
    <h1 class="text-2xl font-bold tracking-tight">Patient Record</h1>
    <p class="text-muted-foreground">
      Manage your clinical information, devices, and insulins
    </p>
  </div>

  <!-- ── Clinical Information ──────────────────────────────────── -->
  <Card.Root>
    <Card.Header>
      <div class="flex items-center gap-2">
        <HeartPulse class="h-5 w-5 text-muted-foreground" />
        <Card.Title>Clinical Information</Card.Title>
      </div>
      <Card.Description>
        Basic information about your diabetes management
      </Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <div class="grid gap-4 sm:grid-cols-2">
        <div class="space-y-2">
          <Label for="diabetes-type">Diabetes Type</Label>
          <Select.Root type="single" bind:value={clinicalDiabetesType}>
            <Select.Trigger id="diabetes-type">
              {clinicalDiabetesType
                ? diabetesTypeLabels[clinicalDiabetesType] ?? clinicalDiabetesType
                : "Select type"}
            </Select.Trigger>
            <Select.Content>
              {#each Object.entries(diabetesTypeLabels) as [value, label]}
                <Select.Item {value} {label} />
              {/each}
            </Select.Content>
          </Select.Root>
        </div>

        {#if clinicalDiabetesType === DiabetesType.Other}
          <div class="space-y-2">
            <Label for="diabetes-type-other">Specify Type</Label>
            <Input
              id="diabetes-type-other"
              bind:value={clinicalDiabetesTypeOther}
              placeholder="e.g. Type 3c"
            />
          </div>
        {/if}

        <div class="space-y-2">
          <Label for="diagnosis-date">Diagnosis Date</Label>
          <Input
            id="diagnosis-date"
            type="date"
            bind:value={clinicalDiagnosisDate}
          />
        </div>

        <div class="space-y-2">
          <Label for="date-of-birth">Date of Birth</Label>
          <Input
            id="date-of-birth"
            type="date"
            bind:value={clinicalDateOfBirth}
          />
        </div>

        <div class="space-y-2">
          <Label for="preferred-name">Preferred Name</Label>
          <Input
            id="preferred-name"
            bind:value={clinicalPreferredName}
            placeholder="How you'd like to be addressed"
          />
        </div>

        <div class="space-y-2">
          <Label for="pronouns">Pronouns</Label>
          <Input
            id="pronouns"
            bind:value={clinicalPronouns}
            placeholder="e.g. she/her, he/him, they/them"
          />
        </div>
      </div>
    </Card.Content>
    <Card.Footer class="border-t pt-6">
      <Button onclick={saveClinicalInfo} disabled={clinicalSaving}>
        {#if clinicalSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
        {:else}
          <Save class="mr-2 h-4 w-4" />
        {/if}
        Save Changes
      </Button>
    </Card.Footer>
  </Card.Root>

  <!-- ── Devices ───────────────────────────────────────────────── -->
  <Card.Root>
    <Card.Header>
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Cpu class="h-5 w-5 text-muted-foreground" />
          <Card.Title>Devices</Card.Title>
        </div>
        <Button size="sm" onclick={() => openDeviceDialog()}>
          <Plus class="mr-2 h-4 w-4" />
          Add Device
        </Button>
      </div>
      <Card.Description>
        Pumps, CGMs, meters, and other devices you use
      </Card.Description>
    </Card.Header>
    <Card.Content>
      {#if (devices.current?.length ?? 0) === 0}
        <p class="text-sm text-muted-foreground py-4 text-center">
          No devices added yet. Add your first device to get started.
        </p>
      {:else}
        <div class="space-y-3">
          {#each devices.current ?? [] as device}
            <div
              class="flex items-center justify-between rounded-lg border p-3"
            >
              <div class="space-y-1 min-w-0 flex-1">
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="font-medium text-sm">
                    {device.manufacturer ?? "Unknown"} {device.model ?? ""}
                  </span>
                  <Badge variant="secondary" class="text-xs">
                    {deviceCategoryLabels[device.deviceCategory ?? ""] ??
                      device.deviceCategory}
                  </Badge>
                  {#if device.isCurrent}
                    <Badge
                      variant="default"
                      class="text-xs bg-green-600 hover:bg-green-700"
                    >
                      Current
                    </Badge>
                  {/if}
                </div>
                <div class="flex items-center gap-3 text-xs text-muted-foreground flex-wrap">
                  {#if device.aidAlgorithm && device.aidAlgorithm !== AidAlgorithm.None}
                    <span>
                      AID: {aidAlgorithmLabels[device.aidAlgorithm] ??
                        device.aidAlgorithm}
                    </span>
                  {/if}
                  {#if device.startDate}
                    <span>From {formatDate(device.startDate)}</span>
                  {/if}
                  {#if device.endDate}
                    <span>Until {formatDate(device.endDate)}</span>
                  {/if}
                  {#if device.serialNumber}
                    <span class="font-mono">SN: {device.serialNumber}</span>
                  {/if}
                </div>
              </div>
              <div class="flex items-center gap-1 ml-2">
                <Button
                  variant="ghost"
                  size="icon"
                  onclick={() => openDeviceDialog(device)}
                >
                  <Pencil class="h-4 w-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  onclick={() => (deviceDeleteId = device.id ?? null)}
                >
                  <Trash2 class="h-4 w-4 text-destructive" />
                </Button>
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </Card.Content>
  </Card.Root>

  <!-- ── Insulins ──────────────────────────────────────────────── -->
  <Card.Root>
    <Card.Header>
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Syringe class="h-5 w-5 text-muted-foreground" />
          <Card.Title>Insulins</Card.Title>
        </div>
        <Button size="sm" onclick={() => openInsulinDialog()}>
          <Plus class="mr-2 h-4 w-4" />
          Add Insulin
        </Button>
      </div>
      <Card.Description>
        Insulin types and brands you use or have used
      </Card.Description>
    </Card.Header>
    <Card.Content>
      {#if (insulins.current?.length ?? 0) === 0}
        <p class="text-sm text-muted-foreground py-4 text-center">
          No insulins added yet. Add your first insulin to get started.
        </p>
      {:else}
        <div class="space-y-3">
          {#each insulins.current ?? [] as insulin}
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
                  onclick={() => openInsulinDialog(insulin)}
                >
                  <Pencil class="h-4 w-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  onclick={() => (insulinDeleteId = insulin.id ?? null)}
                >
                  <Trash2 class="h-4 w-4 text-destructive" />
                </Button>
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </Card.Content>
  </Card.Root>
</div>

<!-- ── Device Dialog ─────────────────────────────────────────────── -->
<Dialog.Root bind:open={deviceDialogOpen}>
  <Dialog.Content class="sm:max-w-lg">
    <Dialog.Header>
      <Dialog.Title>
        {deviceEditing ? "Edit Device" : "Add Device"}
      </Dialog.Title>
      <Dialog.Description>
        {deviceEditing
          ? "Update the details of this device."
          : "Add a new device to your patient record."}
      </Dialog.Description>
    </Dialog.Header>

    <div class="space-y-4 py-4">
      <div class="space-y-2">
        <Label for="device-category">Category</Label>
        <Select.Root type="single" bind:value={deviceCategory}>
          <Select.Trigger id="device-category">
            {deviceCategoryLabels[deviceCategory] ?? deviceCategory}
          </Select.Trigger>
          <Select.Content>
            {#each Object.entries(deviceCategoryLabels) as [value, label]}
              <Select.Item {value} {label} />
            {/each}
          </Select.Content>
        </Select.Root>
      </div>

      <div class="grid gap-4 sm:grid-cols-2">
        <div class="space-y-2">
          <Label for="device-manufacturer">Manufacturer</Label>
          <Input
            id="device-manufacturer"
            bind:value={deviceManufacturer}
            placeholder="e.g. Medtronic, Dexcom"
          />
        </div>
        <div class="space-y-2">
          <Label for="device-model">Model</Label>
          <Input
            id="device-model"
            bind:value={deviceModel}
            placeholder="e.g. 780G, G7"
          />
        </div>
      </div>

      {#if showAidAlgorithm}
        <div class="space-y-2">
          <Label for="device-aid">AID Algorithm</Label>
          <Select.Root type="single" bind:value={deviceAidAlgorithm}>
            <Select.Trigger id="device-aid">
              {deviceAidAlgorithm
                ? aidAlgorithmLabels[deviceAidAlgorithm] ?? deviceAidAlgorithm
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

      <div class="space-y-2">
        <Label for="device-serial">Serial Number</Label>
        <Input
          id="device-serial"
          bind:value={deviceSerialNumber}
          placeholder="Optional"
        />
      </div>

      <div class="grid gap-4 sm:grid-cols-2">
        <div class="space-y-2">
          <Label for="device-start">Start Date</Label>
          <Input
            id="device-start"
            type="date"
            bind:value={deviceStartDate}
          />
        </div>
        <div class="space-y-2">
          <Label for="device-end">End Date</Label>
          <Input
            id="device-end"
            type="date"
            bind:value={deviceEndDate}
          />
        </div>
      </div>

      <div class="flex items-center gap-2">
        <input
          id="device-current"
          type="checkbox"
          bind:checked={deviceIsCurrent}
          class="h-4 w-4 rounded border-input"
        />
        <Label for="device-current">Currently in use</Label>
      </div>

      <div class="space-y-2">
        <Label for="device-notes">Notes</Label>
        <Textarea
          id="device-notes"
          bind:value={deviceNotes}
          placeholder="Any additional notes about this device"
          rows={2}
        />
      </div>
    </div>

    <Dialog.Footer>
      <Button
        variant="outline"
        onclick={() => (deviceDialogOpen = false)}
      >
        Cancel
      </Button>
      <Button onclick={saveDevice} disabled={deviceSaving}>
        {#if deviceSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
        {:else}
          <Save class="mr-2 h-4 w-4" />
        {/if}
        {deviceEditing ? "Update" : "Add"} Device
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- ── Device Delete Confirmation ────────────────────────────────── -->
<AlertDialog.Root
  open={deviceDeleteId !== null}
  onOpenChange={(open) => {
    if (!open) deviceDeleteId = null;
  }}
>
  <AlertDialog.Content>
    <AlertDialog.Header>
      <AlertDialog.Title>Delete Device</AlertDialog.Title>
      <AlertDialog.Description>
        Are you sure you want to delete this device? This action cannot be
        undone.
      </AlertDialog.Description>
    </AlertDialog.Header>
    <AlertDialog.Footer>
      <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
      <AlertDialog.Action
        class="bg-destructive text-destructive-foreground hover:bg-destructive/90"
        onclick={deleteDevice}
      >
        Delete
      </AlertDialog.Action>
    </AlertDialog.Footer>
  </AlertDialog.Content>
</AlertDialog.Root>

<!-- ── Insulin Dialog ────────────────────────────────────────────── -->
<Dialog.Root bind:open={insulinDialogOpen}>
  <Dialog.Content class="sm:max-w-lg">
    <Dialog.Header>
      <Dialog.Title>
        {insulinEditing ? "Edit Insulin" : "Add Insulin"}
      </Dialog.Title>
      <Dialog.Description>
        {insulinEditing
          ? "Update the details of this insulin."
          : "Add an insulin to your patient record."}
      </Dialog.Description>
    </Dialog.Header>

    <div class="space-y-4 py-4">
      <div class="space-y-2">
        <Label for="insulin-category">Category</Label>
        <Select.Root type="single" bind:value={insulinCategory}>
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
          id="insulin-name"
          bind:value={insulinName}
          placeholder="e.g. Humalog, Tresiba, Fiasp"
        />
      </div>

      <div class="grid gap-4 sm:grid-cols-2">
        <div class="space-y-2">
          <Label for="insulin-start">Start Date</Label>
          <Input
            id="insulin-start"
            type="date"
            bind:value={insulinStartDate}
          />
        </div>
        <div class="space-y-2">
          <Label for="insulin-end">End Date</Label>
          <Input
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
          bind:checked={insulinIsCurrent}
          class="h-4 w-4 rounded border-input"
        />
        <Label for="insulin-current">Currently in use</Label>
      </div>

      <div class="space-y-2">
        <Label for="insulin-notes">Notes</Label>
        <Textarea
          id="insulin-notes"
          bind:value={insulinNotes}
          placeholder="Any additional notes about this insulin"
          rows={2}
        />
      </div>
    </div>

    <Dialog.Footer>
      <Button
        variant="outline"
        onclick={() => (insulinDialogOpen = false)}
      >
        Cancel
      </Button>
      <Button onclick={saveInsulin} disabled={insulinSaving}>
        {#if insulinSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
        {:else}
          <Save class="mr-2 h-4 w-4" />
        {/if}
        {insulinEditing ? "Update" : "Add"} Insulin
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- ── Insulin Delete Confirmation ───────────────────────────────── -->
<AlertDialog.Root
  open={insulinDeleteId !== null}
  onOpenChange={(open) => {
    if (!open) insulinDeleteId = null;
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
        onclick={deleteInsulin}
      >
        Delete
      </AlertDialog.Action>
    </AlertDialog.Footer>
  </AlertDialog.Content>
</AlertDialog.Root>
