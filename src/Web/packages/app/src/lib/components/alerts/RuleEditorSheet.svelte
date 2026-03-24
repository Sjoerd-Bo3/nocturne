<script lang="ts">
  import { onMount } from "svelte";
  import {
    createRule,
    updateRule,
  } from "$api/generated/alertRules.generated.remote";
  import {
    getSounds,
    deleteSound,
  } from "$api/generated/alertSounds.generated.remote";
  import type {
    AlertRuleResponse,
    AlertCustomSoundResponse,
    CreateAlertScheduleRequest,
    CreateAlertEscalationStepRequest,
    CreateAlertStepChannelRequest,
  } from "$api-clients";
  import * as Sheet from "$lib/components/ui/sheet";
  import * as Tabs from "$lib/components/ui/tabs";
  import * as Select from "$lib/components/ui/select";
  import { Input } from "$lib/components/ui/input";
  import { Button } from "$lib/components/ui/button";
  import { Switch } from "$lib/components/ui/switch";
  import { Label } from "$lib/components/ui/label";
  import { Badge } from "$lib/components/ui/badge";
  import { Separator } from "$lib/components/ui/separator";
  import { Slider } from "$lib/components/ui/slider";
  import {
    Loader2,
    Play,
    Upload,
    Plus,
    X,
    Trash2,
    ChevronDown,
    ChevronUp,
  } from "lucide-svelte";

  interface Props {
    open: boolean;
    rule: AlertRuleResponse | null;
    onSave: () => void;
  }

  let { open = $bindable(), rule, onSave }: Props = $props();

  // --- Audio config type ---
  interface AudioConfig {
    enabled: boolean;
    sound: string;
    customSoundId: string | null;
    ascending: boolean;
    startVolume: number;
    maxVolume: number;
    ascendDurationSeconds: number;
    repeatCount: number;
  }

  interface VisualConfig {
    flashEnabled: boolean;
    flashColor: string;
    persistentBanner: boolean;
    wakeScreen: boolean;
  }

  interface SnoozeConfig {
    defaultMinutes: number;
    options: number[];
    maxCount: number;
    smartSnooze: boolean;
    smartSnoozeExtendMinutes: number;
  }

  interface ClientConfiguration {
    audio: AudioConfig;
    visual: VisualConfig;
    snooze: SnoozeConfig;
  }

  // --- Schedule editing types ---
  interface EditableChannel {
    channelType: string;
    destination: string;
    destinationLabel: string;
  }

  interface EditableStep {
    stepOrder: number;
    delaySeconds: number;
    channels: EditableChannel[];
  }

  interface EditableSchedule {
    name: string;
    isDefault: boolean;
    daysOfWeek: number[];
    startTime: string;
    endTime: string;
    timezone: string;
    escalationSteps: EditableStep[];
    expanded: boolean;
  }

  // --- Defaults ---
  function defaultClientConfig(): ClientConfiguration {
    return {
      audio: {
        enabled: true,
        sound: "alarm-default",
        customSoundId: null,
        ascending: false,
        startVolume: 50,
        maxVolume: 80,
        ascendDurationSeconds: 30,
        repeatCount: 2,
      },
      visual: {
        flashEnabled: false,
        flashColor: "#ff0000",
        persistentBanner: true,
        wakeScreen: false,
      },
      snooze: {
        defaultMinutes: 15,
        options: [5, 15, 30, 60],
        maxCount: 5,
        smartSnooze: false,
        smartSnoozeExtendMinutes: 10,
      },
    };
  }

  function defaultSchedule(): EditableSchedule {
    return {
      name: "Default Schedule",
      isDefault: true,
      daysOfWeek: [],
      startTime: "00:00",
      endTime: "23:59",
      timezone: "UTC",
      escalationSteps: [
        {
          stepOrder: 0,
          delaySeconds: 0,
          channels: [
            {
              channelType: "web_push",
              destination: "",
              destinationLabel: "",
            },
          ],
        },
      ],
      expanded: true,
    };
  }

  // --- State ---
  let activeTab = $state<string>("general");
  let saving = $state(false);
  let customSounds = $state<AlertCustomSoundResponse[]>([]);
  let uploadError = $state<string | null>(null);
  let uploading = $state(false);

  // General tab
  let name = $state("");
  let description = $state("");
  let severity = $state("normal");
  let conditionType = $state("threshold");
  let isComposite = $state(false);

  // Condition params
  let thresholdDirection = $state("below");
  let thresholdValue = $state(70);
  let rocDirection = $state("falling");
  let rocRate = $state(3.0);
  let signalLossTimeout = $state(15);

  let hysteresisMinutes = $state(5);
  let confirmationReadings = $state(1);
  let sortOrder = $state(0);
  let isEnabled = $state(true);

  // Presentation tab
  let clientConfig = $state<ClientConfiguration>(defaultClientConfig());

  // Snooze tab - new option input
  let newSnoozeOption = $state("");

  // Schedules tab
  let schedules = $state<EditableSchedule[]>([defaultSchedule()]);

  // Audio preview
  let previewAudio: HTMLAudioElement | null = null;

  // --- Computed ---
  let isEditMode = $derived(rule !== null);
  let title = $derived(isEditMode ? "Edit Rule" : "Create Rule");

  // Audio slider values need to be arrays for the Slider component
  let startVolumeArr = $derived([clientConfig.audio.startVolume]);
  let maxVolumeArr = $derived([clientConfig.audio.maxVolume]);

  // --- Built-in sounds ---
  const builtInSounds = [
    { value: "alarm-default", label: "Default Alarm" },
    { value: "alarm-urgent", label: "Urgent Alarm" },
    { value: "alarm-high", label: "High Alarm" },
    { value: "alarm-low", label: "Low Alarm" },
    { value: "alert", label: "Alert" },
    { value: "chime", label: "Chime" },
    { value: "bell", label: "Bell" },
    { value: "siren", label: "Siren" },
    { value: "beep", label: "Beep" },
    { value: "soft", label: "Soft" },
  ];

  const dayLabels = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

  // --- Initialization ---
  function initFromRule(r: AlertRuleResponse | null) {
    if (r) {
      name = r.name ?? "";
      description = r.description ?? "";
      severity = r.severity ?? "normal";
      isEnabled = r.isEnabled ?? true;
      hysteresisMinutes = r.hysteresisMinutes ?? 5;
      confirmationReadings = r.confirmationReadings ?? 1;
      sortOrder = r.sortOrder ?? 0;

      // Condition type
      const ct = r.conditionType ?? "threshold";
      if (ct === "composite") {
        isComposite = true;
        conditionType = "composite";
      } else if (ct === "threshold_low" || ct === "threshold_high") {
        isComposite = false;
        conditionType = "threshold";
      } else if (ct === "rate_of_change") {
        isComposite = false;
        conditionType = "rate_of_change";
      } else if (ct === "signal_loss") {
        isComposite = false;
        conditionType = "signal_loss";
      } else {
        isComposite = false;
        conditionType = ct;
      }

      // Condition params
      const params = r.conditionParams;
      if (params) {
        if (conditionType === "threshold") {
          if (r.conditionType === "threshold_high") {
            thresholdDirection = "above";
          } else {
            thresholdDirection = "below";
          }
          thresholdValue = params.threshold ?? params.value ?? 70;
        } else if (conditionType === "rate_of_change") {
          rocDirection = params.direction ?? "falling";
          rocRate = params.rateThreshold ?? params.rate ?? 3.0;
        } else if (conditionType === "signal_loss") {
          signalLossTimeout = params.minutes ?? params.timeout_minutes ?? 15;
        }
      }

      // Client configuration
      const cc = r.clientConfiguration;
      if (cc) {
        clientConfig = {
          audio: {
            enabled: cc.audio?.enabled ?? true,
            sound: cc.audio?.sound ?? "alarm-default",
            customSoundId: cc.audio?.customSoundId ?? null,
            ascending: cc.audio?.ascending ?? false,
            startVolume: cc.audio?.startVolume ?? 50,
            maxVolume: cc.audio?.maxVolume ?? 80,
            ascendDurationSeconds: cc.audio?.ascendDurationSeconds ?? 30,
            repeatCount: cc.audio?.repeatCount ?? 2,
          },
          visual: {
            flashEnabled: cc.visual?.flashEnabled ?? false,
            flashColor: cc.visual?.flashColor ?? "#ff0000",
            persistentBanner: cc.visual?.persistentBanner ?? true,
            wakeScreen: cc.visual?.wakeScreen ?? false,
          },
          snooze: {
            defaultMinutes: cc.snooze?.defaultMinutes ?? 15,
            options: cc.snooze?.options ?? [5, 15, 30, 60],
            maxCount: cc.snooze?.maxCount ?? 5,
            smartSnooze: cc.snooze?.smartSnooze ?? false,
            smartSnoozeExtendMinutes:
              cc.snooze?.smartSnoozeExtendMinutes ?? 10,
          },
        };
      } else {
        clientConfig = defaultClientConfig();
      }

      // Schedules
      if (r.schedules && r.schedules.length > 0) {
        schedules = r.schedules.map((s) => ({
          name: s.name ?? "Default Schedule",
          isDefault: s.isDefault ?? false,
          daysOfWeek: s.daysOfWeek ?? [],
          startTime: s.startTime ?? "00:00",
          endTime: s.endTime ?? "23:59",
          timezone: s.timezone ?? "UTC",
          escalationSteps: (s.escalationSteps ?? [])
            .sort((a, b) => (a.stepOrder ?? 0) - (b.stepOrder ?? 0))
            .map((step) => ({
              stepOrder: step.stepOrder ?? 0,
              delaySeconds: step.delaySeconds ?? 0,
              channels: (step.channels ?? []).map((ch) => ({
                channelType: ch.channelType ?? "web_push",
                destination: ch.destination ?? "",
                destinationLabel: ch.destinationLabel ?? "",
              })),
            })),
          expanded: false,
        }));
      } else {
        schedules = [defaultSchedule()];
      }
    } else {
      // Create mode defaults
      name = "";
      description = "";
      severity = "normal";
      conditionType = "threshold";
      isComposite = false;
      thresholdDirection = "below";
      thresholdValue = 70;
      rocDirection = "falling";
      rocRate = 3.0;
      signalLossTimeout = 15;
      hysteresisMinutes = 5;
      confirmationReadings = 1;
      sortOrder = 0;
      isEnabled = true;
      clientConfig = defaultClientConfig();
      schedules = [defaultSchedule()];
    }
    activeTab = "general";
    newSnoozeOption = "";
    uploadError = null;
  }

  $effect(() => {
    if (open) {
      initFromRule(rule);
    }
  });

  // Load custom sounds on mount
  onMount(async () => {
    try {
      const result = await getSounds();
      customSounds = Array.isArray(result) ? result : [];
    } catch {
      // Sounds unavailable
    }
  });

  // --- Condition type mapping ---
  function getApiConditionType(): string {
    if (conditionType === "threshold") {
      return thresholdDirection === "above" ? "threshold_high" : "threshold_low";
    }
    return conditionType;
  }

  function getConditionParams(): Record<string, unknown> {
    switch (conditionType) {
      case "threshold":
        return { direction: thresholdDirection, value: thresholdValue, threshold: thresholdValue };
      case "rate_of_change":
        return { direction: rocDirection, rate: rocRate, rateThreshold: rocRate };
      case "signal_loss":
        return { timeout_minutes: signalLossTimeout, minutes: signalLossTimeout };
      default:
        return {};
    }
  }

  // --- Save ---
  async function handleSave() {
    saving = true;
    try {
      const schedulesPayload: CreateAlertScheduleRequest[] = schedules.map(
        (s) => ({
          name: s.name || undefined,
          isDefault: s.isDefault,
          daysOfWeek:
            s.daysOfWeek.length === 0 || s.daysOfWeek.length === 7
              ? undefined
              : s.daysOfWeek,
          startTime: s.isDefault ? undefined : s.startTime || undefined,
          endTime: s.isDefault ? undefined : s.endTime || undefined,
          timezone: s.timezone || undefined,
          escalationSteps: s.escalationSteps.map(
            (step): CreateAlertEscalationStepRequest => ({
              stepOrder: step.stepOrder,
              delaySeconds: step.delaySeconds,
              channels: step.channels.map(
                (ch): CreateAlertStepChannelRequest => ({
                  channelType: ch.channelType,
                  destination: ch.destination || undefined,
                  destinationLabel: ch.destinationLabel || undefined,
                }),
              ),
            }),
          ),
        }),
      );

      const payload = {
        name,
        description: description || undefined,
        conditionType: isComposite ? "composite" : getApiConditionType(),
        conditionParams: isComposite ? rule?.conditionParams : getConditionParams(),
        hysteresisMinutes,
        confirmationReadings,
        isEnabled,
        sortOrder,
        severity: severity || undefined,
        clientConfiguration: clientConfig,
        schedules: schedulesPayload,
      };

      if (isEditMode && rule?.id) {
        await updateRule({ id: rule.id, request: payload });
      } else {
        await createRule(payload);
      }

      onSave();
      open = false;
    } catch {
      // Error handled by remote function
    } finally {
      saving = false;
    }
  }

  // --- Audio preview ---
  function playPreview() {
    if (previewAudio) {
      previewAudio.pause();
      previewAudio = null;
    }
    try {
      const sound = clientConfig.audio.sound;
      const isCustom =
        clientConfig.audio.customSoundId &&
        customSounds.some((s) => s.id === clientConfig.audio.customSoundId);
      const url = isCustom
        ? `/api/v4/alert-sounds/${clientConfig.audio.customSoundId}/stream`
        : `/sounds/${sound}.mp3`;
      previewAudio = new Audio(url);
      previewAudio.volume = (clientConfig.audio.maxVolume ?? 80) / 100;
      previewAudio.play().catch(() => {
        // Audio file may not exist yet
      });
    } catch {
      // Gracefully handle missing files
    }
  }

  // --- File upload ---
  async function handleFileUpload(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    uploadError = null;

    if (file.size > 512000) {
      uploadError = "File size must be less than 500KB";
      input.value = "";
      return;
    }

    uploading = true;
    try {
      // Upload via fetch since the generated remote function doesn't support FormData params
      const formData = new FormData();
      formData.append("file", file);
      const response = await fetch("/api/v4/alert-sounds", {
        method: "POST",
        body: formData,
      });
      if (!response.ok) {
        uploadError = "Failed to upload sound";
      } else {
        const result = await getSounds();
        customSounds = Array.isArray(result) ? result : [];
      }
    } catch {
      uploadError = "Failed to upload sound";
    } finally {
      uploading = false;
      input.value = "";
    }
  }

  // --- Custom sound deletion ---
  async function handleDeleteSound(id: string) {
    try {
      await deleteSound(id);
      const result = await getSounds();
      customSounds = Array.isArray(result) ? result : [];
      // If the deleted sound was selected, reset to default
      if (clientConfig.audio.customSoundId === id) {
        clientConfig.audio.customSoundId = null;
        clientConfig.audio.sound = "alarm-default";
      }
    } catch {
      // Error handled by remote function
    }
  }

  // --- Snooze options ---
  function addSnoozeOption() {
    const val = parseInt(newSnoozeOption, 10);
    if (!isNaN(val) && val > 0 && !clientConfig.snooze.options.includes(val)) {
      clientConfig.snooze.options = [
        ...clientConfig.snooze.options,
        val,
      ].sort((a, b) => a - b);
      newSnoozeOption = "";
    }
  }

  function removeSnoozeOption(val: number) {
    clientConfig.snooze.options = clientConfig.snooze.options.filter(
      (o) => o !== val,
    );
  }

  // --- Schedule management ---
  function addSchedule() {
    const newSched = defaultSchedule();
    newSched.isDefault = false;
    newSched.name = `Schedule ${schedules.length + 1}`;
    schedules = [...schedules, newSched];
  }

  function removeSchedule(index: number) {
    if (schedules.length <= 1) return;
    schedules = schedules.filter((_, i) => i !== index);
  }

  function toggleScheduleDefault(index: number) {
    schedules = schedules.map((s, i) => ({
      ...s,
      isDefault: i === index,
      expanded: s.expanded,
    }));
  }

  function toggleScheduleExpand(index: number) {
    schedules = schedules.map((s, i) => ({
      ...s,
      expanded: i === index ? !s.expanded : s.expanded,
    }));
  }

  function toggleDay(schedIndex: number, day: number) {
    const sched = schedules[schedIndex];
    if (sched.daysOfWeek.includes(day)) {
      sched.daysOfWeek = sched.daysOfWeek.filter((d) => d !== day);
    } else {
      sched.daysOfWeek = [...sched.daysOfWeek, day].sort();
    }
    schedules = [...schedules];
  }

  // --- Escalation step management ---
  function addStep(schedIndex: number) {
    const sched = schedules[schedIndex];
    const newStep: EditableStep = {
      stepOrder: sched.escalationSteps.length,
      delaySeconds: 60,
      channels: [],
    };
    sched.escalationSteps = [...sched.escalationSteps, newStep];
    schedules = [...schedules];
  }

  function removeStep(schedIndex: number, stepIndex: number) {
    const sched = schedules[schedIndex];
    if (stepIndex === 0) return;
    sched.escalationSteps = sched.escalationSteps
      .filter((_, i) => i !== stepIndex)
      .map((s, i) => ({ ...s, stepOrder: i }));
    schedules = [...schedules];
  }

  function addChannel(schedIndex: number, stepIndex: number) {
    const step = schedules[schedIndex].escalationSteps[stepIndex];
    step.channels = [
      ...step.channels,
      { channelType: "web_push", destination: "", destinationLabel: "" },
    ];
    schedules = [...schedules];
  }

  function removeChannel(
    schedIndex: number,
    stepIndex: number,
    channelIndex: number,
  ) {
    const step = schedules[schedIndex].escalationSteps[stepIndex];
    step.channels = step.channels.filter((_, i) => i !== channelIndex);
    schedules = [...schedules];
  }

  // --- Sound selection helpers ---
  function getSelectedSoundLabel(): string {
    if (clientConfig.audio.customSoundId) {
      const custom = customSounds.find(
        (s) => s.id === clientConfig.audio.customSoundId,
      );
      if (custom) return custom.name ?? "Custom Sound";
    }
    const built = builtInSounds.find(
      (s) => s.value === clientConfig.audio.sound,
    );
    return built?.label ?? clientConfig.audio.sound;
  }

  function handleSoundSelect(value: string) {
    const custom = customSounds.find((s) => s.id === value);
    if (custom) {
      clientConfig.audio.customSoundId = custom.id ?? null;
      clientConfig.audio.sound = custom.name ?? "custom";
    } else {
      clientConfig.audio.customSoundId = null;
      clientConfig.audio.sound = value;
    }
  }

  // Map condition type labels
  const conditionTypeLabels: Record<string, string> = {
    threshold: "Threshold",
    rate_of_change: "Rate of Change",
    signal_loss: "Signal Loss",
  };

  const severityLabels: Record<string, string> = {
    normal: "Normal",
    critical: "Critical",
  };

  const thresholdDirLabels: Record<string, string> = {
    below: "Below",
    above: "Above",
  };

  const rocDirLabels: Record<string, string> = {
    falling: "Falling",
    rising: "Rising",
  };

  const channelTypeLabels: Record<string, string> = {
    web_push: "Web Push",
    webhook: "Webhook",
  };
</script>

<Sheet.Root bind:open>
  <Sheet.Content side="right" class="w-full sm:max-w-xl overflow-y-auto">
    <Sheet.Header>
      <Sheet.Title>{title}</Sheet.Title>
      <Sheet.Description>
        {isEditMode
          ? "Modify the alert rule configuration"
          : "Configure a new alert rule"}
      </Sheet.Description>
    </Sheet.Header>

    <div class="flex-1 overflow-y-auto px-1">
      <Tabs.Root bind:value={activeTab}>
        <Tabs.List class="w-full">
          <Tabs.Trigger value="general" class="flex-1">General</Tabs.Trigger>
          <Tabs.Trigger value="presentation" class="flex-1"
            >Presentation</Tabs.Trigger
          >
          <Tabs.Trigger value="snooze" class="flex-1">Snooze</Tabs.Trigger>
          <Tabs.Trigger value="schedules" class="flex-1"
            >Schedules</Tabs.Trigger
          >
        </Tabs.List>

        <!-- General Tab -->
        <Tabs.Content value="general" class="space-y-4 pt-4">
          <div class="space-y-2">
            <Label for="rule-name">Name</Label>
            <Input id="rule-name" bind:value={name} placeholder="Rule name" />
          </div>

          <div class="space-y-2">
            <Label for="rule-description">Description (optional)</Label>
            <Input
              id="rule-description"
              bind:value={description}
              placeholder="Brief description"
            />
          </div>

          <div class="space-y-2">
            <Label for="rule-severity">Severity</Label>
            <Select.Root type="single" bind:value={severity}>
              <Select.Trigger id="rule-severity">
                {severityLabels[severity] ?? severity}
              </Select.Trigger>
              <Select.Content>
                <Select.Item value="normal" label="Normal" />
                <Select.Item value="critical" label="Critical" />
              </Select.Content>
            </Select.Root>
            {#if severity === "critical"}
              <p class="text-xs text-muted-foreground">
                Critical alerts bypass quiet hours
              </p>
            {/if}
          </div>

          <Separator />

          <div class="space-y-2">
            <Label for="rule-condition-type">Condition Type</Label>
            {#if isComposite}
              <div>
                <Badge variant="secondary">
                  Composite -- editing not available
                </Badge>
              </div>
            {:else}
              <Select.Root type="single" bind:value={conditionType}>
                <Select.Trigger id="rule-condition-type">
                  {conditionTypeLabels[conditionType] ?? conditionType}
                </Select.Trigger>
                <Select.Content>
                  <Select.Item value="threshold" label="Threshold" />
                  <Select.Item
                    value="rate_of_change"
                    label="Rate of Change"
                  />
                  <Select.Item value="signal_loss" label="Signal Loss" />
                </Select.Content>
              </Select.Root>
            {/if}
          </div>

          {#if !isComposite}
            <div class="space-y-3 rounded-md border p-3 bg-muted/30">
              {#if conditionType === "threshold"}
                <div class="space-y-2">
                  <Label for="threshold-direction">Direction</Label>
                  <Select.Root
                    type="single"
                    bind:value={thresholdDirection}
                  >
                    <Select.Trigger id="threshold-direction">
                      {thresholdDirLabels[thresholdDirection] ??
                        thresholdDirection}
                    </Select.Trigger>
                    <Select.Content>
                      <Select.Item value="below" label="Below" />
                      <Select.Item value="above" label="Above" />
                    </Select.Content>
                  </Select.Root>
                </div>
                <div class="space-y-2">
                  <Label for="threshold-value">Value (mg/dL)</Label>
                  <Input
                    id="threshold-value"
                    type="number"
                    bind:value={thresholdValue}
                  />
                </div>
              {:else if conditionType === "rate_of_change"}
                <div class="space-y-2">
                  <Label for="roc-direction">Direction</Label>
                  <Select.Root type="single" bind:value={rocDirection}>
                    <Select.Trigger id="roc-direction">
                      {rocDirLabels[rocDirection] ?? rocDirection}
                    </Select.Trigger>
                    <Select.Content>
                      <Select.Item value="falling" label="Falling" />
                      <Select.Item value="rising" label="Rising" />
                    </Select.Content>
                  </Select.Root>
                </div>
                <div class="space-y-2">
                  <Label for="roc-rate">Rate (mg/dL/min)</Label>
                  <Input id="roc-rate" type="number" step="0.1" bind:value={rocRate} />
                </div>
              {:else if conditionType === "signal_loss"}
                <div class="space-y-2">
                  <Label for="signal-loss-timeout">Timeout (minutes)</Label>
                  <Input
                    id="signal-loss-timeout"
                    type="number"
                    bind:value={signalLossTimeout}
                  />
                </div>
              {/if}
            </div>
          {/if}

          <Separator />

          <div class="grid grid-cols-2 gap-4">
            <div class="space-y-2">
              <Label for="hysteresis">Hysteresis (minutes)</Label>
              <Input
                id="hysteresis"
                type="number"
                bind:value={hysteresisMinutes}
              />
            </div>
            <div class="space-y-2">
              <Label for="confirmation">Confirmation Readings</Label>
              <Input
                id="confirmation"
                type="number"
                bind:value={confirmationReadings}
              />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div class="space-y-2">
              <Label for="sort-order">Sort Order</Label>
              <Input
                id="sort-order"
                type="number"
                bind:value={sortOrder}
              />
            </div>
            <div class="flex items-end gap-3 pb-1">
              <div class="space-y-2">
                <Label>Enabled</Label>
                <Switch bind:checked={isEnabled} />
              </div>
            </div>
          </div>
        </Tabs.Content>

        <!-- Presentation Tab -->
        <Tabs.Content value="presentation" class="space-y-6 pt-4">
          <!-- Audio Section -->
          <div class="space-y-4">
            <h3 class="text-sm font-medium">Audio</h3>

            <div class="flex items-center justify-between">
              <Label>Audio Enabled</Label>
              <Switch bind:checked={clientConfig.audio.enabled} />
            </div>

            {#if clientConfig.audio.enabled}
              <div class="space-y-2">
                <Label for="audio-sound">Sound</Label>
                <div class="flex gap-2">
                  <div class="flex-1">
                    <Select.Root
                      type="single"
                      value={clientConfig.audio.customSoundId ?? clientConfig.audio.sound}
                      onValueChange={handleSoundSelect}
                    >
                      <Select.Trigger id="audio-sound">
                        {getSelectedSoundLabel()}
                      </Select.Trigger>
                      <Select.Content>
                        <Select.Group>
                          <Select.Label>Built-in Sounds</Select.Label>
                          {#each builtInSounds as sound}
                            <Select.Item
                              value={sound.value}
                              label={sound.label}
                            />
                          {/each}
                        </Select.Group>
                        {#if customSounds.length > 0}
                          <Select.Separator />
                          <Select.Group>
                            <Select.Label>Custom Sounds</Select.Label>
                            {#each customSounds as sound}
                              <Select.Item
                                value={sound.id ?? ""}
                                label={sound.name ?? "Custom"}
                              />
                            {/each}
                          </Select.Group>
                        {/if}
                      </Select.Content>
                    </Select.Root>
                  </div>
                  <Button
                    variant="outline"
                    size="icon"
                    onclick={playPreview}
                    title="Preview sound"
                  >
                    <Play class="h-4 w-4" />
                  </Button>
                </div>
              </div>

              <!-- Upload custom sound -->
              <div class="space-y-2">
                <Label>Upload Custom Sound</Label>
                <div class="flex items-center gap-2">
                  <label
                    class="flex items-center gap-2 cursor-pointer rounded-md border px-3 py-2 text-sm hover:bg-muted transition-colors"
                  >
                    {#if uploading}
                      <Loader2 class="h-4 w-4 animate-spin" />
                    {:else}
                      <Upload class="h-4 w-4" />
                    {/if}
                    <span>Choose file</span>
                    <input
                      type="file"
                      accept="audio/*"
                      class="hidden"
                      onchange={handleFileUpload}
                      disabled={uploading}
                    />
                  </label>
                  <span class="text-xs text-muted-foreground">Max 500KB</span>
                </div>
                {#if uploadError}
                  <p class="text-xs text-destructive">{uploadError}</p>
                {/if}
              </div>

              <!-- Custom sounds list with delete -->
              {#if customSounds.length > 0}
                <div class="space-y-1">
                  <Label>Custom Sounds</Label>
                  {#each customSounds as sound}
                    <div
                      class="flex items-center justify-between p-2 rounded-md border text-sm"
                    >
                      <span>{sound.name ?? "Custom"}</span>
                      <Button
                        variant="ghost"
                        size="icon"
                        class="h-7 w-7 text-destructive"
                        onclick={() => handleDeleteSound(sound.id ?? "")}
                      >
                        <Trash2 class="h-3 w-3" />
                      </Button>
                    </div>
                  {/each}
                </div>
              {/if}

              <Separator />

              <div class="flex items-center justify-between">
                <Label>Ascending Volume</Label>
                <Switch bind:checked={clientConfig.audio.ascending} />
              </div>

              {#if clientConfig.audio.ascending}
                <div class="space-y-2">
                  <Label>Start Volume: {clientConfig.audio.startVolume}%</Label>
                  <Slider
                    type="multiple"
                    value={startVolumeArr}
                    onValueChange={(v: number[]) => {
                      clientConfig.audio.startVolume = v[0];
                    }}
                    min={0}
                    max={100}
                    step={1}
                  />
                </div>
              {/if}

              <div class="space-y-2">
                <Label>Max Volume: {clientConfig.audio.maxVolume}%</Label>
                <Slider
                  type="multiple"
                  value={maxVolumeArr}
                  onValueChange={(v: number[]) => {
                    clientConfig.audio.maxVolume = v[0];
                  }}
                  min={0}
                  max={100}
                  step={1}
                />
              </div>

              <div class="grid grid-cols-2 gap-4">
                <div class="space-y-2">
                  <Label for="ascend-duration">Ascend Duration (s)</Label>
                  <Input
                    id="ascend-duration"
                    type="number"
                    bind:value={clientConfig.audio.ascendDurationSeconds}
                  />
                </div>
                <div class="space-y-2">
                  <Label for="repeat-count">Repeat Count</Label>
                  <Input
                    id="repeat-count"
                    type="number"
                    bind:value={clientConfig.audio.repeatCount}
                  />
                </div>
              </div>
            {/if}
          </div>

          <Separator />

          <!-- Visual Section -->
          <div class="space-y-4">
            <h3 class="text-sm font-medium">Visual</h3>

            <div class="flex items-center justify-between">
              <Label>Screen Flash</Label>
              <Switch bind:checked={clientConfig.visual.flashEnabled} />
            </div>

            {#if clientConfig.visual.flashEnabled}
              <div class="space-y-2">
                <Label for="flash-color">Flash Color</Label>
                <input
                  id="flash-color"
                  type="color"
                  bind:value={clientConfig.visual.flashColor}
                  class="h-9 w-16 rounded-md border cursor-pointer"
                />
              </div>
            {/if}

            <div class="flex items-center justify-between">
              <Label>Persistent Banner</Label>
              <Switch bind:checked={clientConfig.visual.persistentBanner} />
            </div>

            <div class="flex items-center justify-between">
              <Label>Wake Screen</Label>
              <Switch bind:checked={clientConfig.visual.wakeScreen} />
            </div>
          </div>
        </Tabs.Content>

        <!-- Snooze Tab -->
        <Tabs.Content value="snooze" class="space-y-4 pt-4">
          <div class="space-y-2">
            <Label for="snooze-default">Default Snooze Duration (minutes)</Label>
            <Input
              id="snooze-default"
              type="number"
              bind:value={clientConfig.snooze.defaultMinutes}
            />
          </div>

          <div class="space-y-2">
            <Label>Snooze Options</Label>
            <div class="flex flex-wrap gap-2">
              {#each clientConfig.snooze.options as opt}
                <Badge variant="secondary" class="gap-1 pr-1">
                  {opt}m
                  <button
                    class="ml-1 rounded-full hover:bg-muted-foreground/20 p-0.5"
                    onclick={() => removeSnoozeOption(opt)}
                  >
                    <X class="h-3 w-3" />
                  </button>
                </Badge>
              {/each}
            </div>
            <div class="flex gap-2">
              <Input
                placeholder="Minutes"
                type="number"
                bind:value={newSnoozeOption}
                class="w-24"
                onkeydown={(e: KeyboardEvent) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    addSnoozeOption();
                  }
                }}
              />
              <Button variant="outline" size="sm" onclick={addSnoozeOption}>
                Add
              </Button>
            </div>
          </div>

          <div class="space-y-2">
            <Label for="snooze-max-count">Max Snooze Count</Label>
            <Input
              id="snooze-max-count"
              type="number"
              bind:value={clientConfig.snooze.maxCount}
            />
          </div>

          <Separator />

          <div class="flex items-center justify-between">
            <Label>Smart Snooze</Label>
            <Switch bind:checked={clientConfig.snooze.smartSnooze} />
          </div>

          {#if clientConfig.snooze.smartSnooze}
            <div class="space-y-2">
              <Label for="smart-snooze-extend"
                >Smart Snooze Extend (minutes)</Label
              >
              <Input
                id="smart-snooze-extend"
                type="number"
                bind:value={clientConfig.snooze.smartSnoozeExtendMinutes}
              />
              <p class="text-xs text-muted-foreground">
                Automatically extends snooze when glucose trend is favorable
              </p>
            </div>
          {/if}
        </Tabs.Content>

        <!-- Schedules Tab -->
        <Tabs.Content value="schedules" class="space-y-4 pt-4">
          {#each schedules as schedule, schedIdx}
            <div class="rounded-md border">
              <!-- Schedule header -->
              <button
                class="flex items-center justify-between w-full p-3 text-left hover:bg-muted/50 transition-colors"
                onclick={() => toggleScheduleExpand(schedIdx)}
              >
                <div class="flex items-center gap-2">
                  <span class="text-sm font-medium">
                    {schedule.name || "Unnamed Schedule"}
                  </span>
                  {#if schedule.isDefault}
                    <Badge variant="secondary">Default</Badge>
                  {/if}
                </div>
                {#if schedule.expanded}
                  <ChevronUp class="h-4 w-4 text-muted-foreground" />
                {:else}
                  <ChevronDown class="h-4 w-4 text-muted-foreground" />
                {/if}
              </button>

              {#if schedule.expanded}
                <div class="border-t p-3 space-y-4">
                  <div class="space-y-2">
                    <Label>Name</Label>
                    <Input
                      bind:value={schedule.name}
                      placeholder="Schedule name"
                    />
                  </div>

                  <div class="flex items-center justify-between">
                    <Label>Default Schedule</Label>
                    <Switch
                      checked={schedule.isDefault}
                      onCheckedChange={() => toggleScheduleDefault(schedIdx)}
                    />
                  </div>

                  {#if !schedule.isDefault}
                    <div class="grid grid-cols-2 gap-4">
                      <div class="space-y-2">
                        <Label>Start Time</Label>
                        <Input type="time" bind:value={schedule.startTime} />
                      </div>
                      <div class="space-y-2">
                        <Label>End Time</Label>
                        <Input type="time" bind:value={schedule.endTime} />
                      </div>
                    </div>
                  {/if}

                  <div class="space-y-2">
                    <Label>Days of Week</Label>
                    <div class="flex gap-1">
                      {#each dayLabels as dayLabel, dayIdx}
                        <button
                          class="h-8 w-10 rounded-md border text-xs font-medium transition-colors {schedule.daysOfWeek.includes(
                            dayIdx,
                          )
                            ? 'bg-primary text-primary-foreground'
                            : 'bg-background hover:bg-muted'}"
                          onclick={() => toggleDay(schedIdx, dayIdx)}
                        >
                          {dayLabel}
                        </button>
                      {/each}
                    </div>
                    <p class="text-xs text-muted-foreground">
                      {schedule.daysOfWeek.length === 0 ||
                      schedule.daysOfWeek.length === 7
                        ? "Every day"
                        : `${schedule.daysOfWeek.map((d) => dayLabels[d]).join(", ")}`}
                    </p>
                  </div>

                  <div class="space-y-2">
                    <Label>Timezone</Label>
                    <Input
                      bind:value={schedule.timezone}
                      placeholder="UTC"
                    />
                  </div>

                  <Separator />

                  <!-- Escalation Steps -->
                  <div class="space-y-3">
                    <h4 class="text-sm font-medium">Escalation Steps</h4>

                    {#each schedule.escalationSteps as step, stepIdx}
                      <div class="relative pl-4 border-l-2 border-muted pb-3">
                        <div class="space-y-3">
                          <div class="flex items-center justify-between">
                            <span class="text-sm font-medium"
                              >Step {stepIdx + 1}</span
                            >
                            {#if stepIdx > 0}
                              <Button
                                variant="ghost"
                                size="icon"
                                class="h-7 w-7 text-destructive"
                                onclick={() =>
                                  removeStep(schedIdx, stepIdx)}
                              >
                                <Trash2 class="h-3 w-3" />
                              </Button>
                            {/if}
                          </div>

                          <div class="space-y-2">
                            <Label>Delay (seconds)</Label>
                            <Input
                              type="number"
                              bind:value={step.delaySeconds}
                              disabled={stepIdx === 0}
                            />
                            {#if stepIdx === 0}
                              <p class="text-xs text-muted-foreground">
                                First step fires immediately
                              </p>
                            {/if}
                          </div>

                          <!-- Channels -->
                          <div class="space-y-2">
                            {#each step.channels as channel, chIdx}
                              <div
                                class="flex items-start gap-2 p-2 rounded-md border bg-background"
                              >
                                <div class="flex-1 space-y-2">
                                  <Select.Root
                                    type="single"
                                    bind:value={channel.channelType}
                                  >
                                    <Select.Trigger>
                                      {channelTypeLabels[
                                        channel.channelType
                                      ] ?? channel.channelType}
                                    </Select.Trigger>
                                    <Select.Content>
                                      <Select.Item
                                        value="web_push"
                                        label="Web Push"
                                      />
                                      <Select.Item
                                        value="webhook"
                                        label="Webhook"
                                      />
                                    </Select.Content>
                                  </Select.Root>
                                  <Input
                                    bind:value={channel.destination}
                                    placeholder="Destination"
                                  />
                                  <Input
                                    bind:value={channel.destinationLabel}
                                    placeholder="Label (optional)"
                                  />
                                </div>
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  class="h-7 w-7 text-destructive shrink-0"
                                  onclick={() =>
                                    removeChannel(
                                      schedIdx,
                                      stepIdx,
                                      chIdx,
                                    )}
                                >
                                  <X class="h-3 w-3" />
                                </Button>
                              </div>
                            {/each}

                            <Button
                              variant="outline"
                              size="sm"
                              onclick={() =>
                                addChannel(schedIdx, stepIdx)}
                            >
                              <Plus class="h-3 w-3 mr-1" />
                              Add Channel
                            </Button>
                          </div>
                        </div>
                      </div>
                    {/each}

                    <Button
                      variant="outline"
                      size="sm"
                      onclick={() => addStep(schedIdx)}
                    >
                      <Plus class="h-3 w-3 mr-1" />
                      Add Step
                    </Button>
                  </div>

                  <Separator />

                  <Button
                    variant="outline"
                    size="sm"
                    class="text-destructive"
                    disabled={schedules.length <= 1}
                    onclick={() => removeSchedule(schedIdx)}
                  >
                    <Trash2 class="h-3 w-3 mr-1" />
                    Remove Schedule
                  </Button>
                </div>
              {/if}
            </div>
          {/each}

          <Button variant="outline" onclick={addSchedule}>
            <Plus class="h-4 w-4 mr-2" />
            Add Schedule
          </Button>
        </Tabs.Content>
      </Tabs.Root>
    </div>

    <Sheet.Footer class="mt-4">
      <Button variant="outline" onclick={() => (open = false)}>Cancel</Button>
      <Button onclick={handleSave} disabled={saving || !name.trim()}>
        {#if saving}
          <Loader2 class="h-4 w-4 mr-2 animate-spin" />
        {/if}
        {isEditMode ? "Update Rule" : "Create Rule"}
      </Button>
    </Sheet.Footer>
  </Sheet.Content>
</Sheet.Root>
