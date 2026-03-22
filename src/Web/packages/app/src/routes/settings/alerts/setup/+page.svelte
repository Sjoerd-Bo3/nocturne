<script lang="ts">
  import { goto } from "$app/navigation";
  import { createRule } from "$api/generated/alertRules.generated.remote";
  import type { CreateAlertRuleRequest } from "$api-clients";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Switch } from "$lib/components/ui/switch";
  import { Badge } from "$lib/components/ui/badge";
  import { Separator } from "$lib/components/ui/separator";
  import {
    ArrowLeft,
    ArrowRight,
    Check,
    TrendingDown,
    TrendingUp,
    Zap,
    WifiOff,
    AlertTriangle,
    Shield,
    Bell,
    Webhook,
    MessageSquare,
    Send,
    Loader2,
  } from "lucide-svelte";

  // Step management
  let currentStep = $state(1);
  const totalSteps = 3;

  // Step 1: Preset selection
  type Preset = {
    key: string;
    name: string;
    description: string;
    icon: typeof TrendingDown;
    conditionType: string;
    conditionParams: Record<string, unknown>;
    threshold: number;
    thresholdUnit: string;
    thresholdField: string;
    confirmationReadings: number;
    hysteresisMinutes: number;
    enabled: boolean;
    severity: string;
    clientConfiguration: Record<string, unknown>;
  };

  let presets = $state<Preset[]>([
    {
      key: "urgent_low",
      name: "Urgent Low",
      description: "Critical low glucose alert for immediate attention",
      icon: AlertTriangle,
      conditionType: "threshold_low",
      conditionParams: { threshold: 54, direction: "below" },
      threshold: 54,
      thresholdUnit: "mg/dL",
      thresholdField: "threshold",
      confirmationReadings: 1,
      hysteresisMinutes: 15,
      enabled: true,
      severity: "critical",
      clientConfiguration: {
        audio: { enabled: true, sound: "alarm-urgent", ascending: true, startVolume: 50, maxVolume: 100, ascendDurationSeconds: 30, repeatCount: 3 },
        visual: { flashEnabled: true, flashColor: "#ff0000", persistentBanner: true, wakeScreen: true },
        snooze: { defaultMinutes: 5, options: [5, 10, 15], maxCount: 3, smartSnooze: true, smartSnoozeExtendMinutes: 5 },
      },
    },
    {
      key: "low",
      name: "Low",
      description: "Low glucose warning before it becomes urgent",
      icon: TrendingDown,
      conditionType: "threshold_low",
      conditionParams: { threshold: 70, direction: "below" },
      threshold: 70,
      thresholdUnit: "mg/dL",
      thresholdField: "threshold",
      confirmationReadings: 2,
      hysteresisMinutes: 15,
      enabled: true,
      severity: "normal",
      clientConfiguration: {
        audio: { enabled: true, sound: "alarm-low", ascending: true, startVolume: 30, maxVolume: 80, ascendDurationSeconds: 30, repeatCount: 2 },
        visual: { flashEnabled: false, flashColor: "#ff0000", persistentBanner: true, wakeScreen: false },
        snooze: { defaultMinutes: 15, options: [5, 15, 30], maxCount: 5, smartSnooze: true, smartSnoozeExtendMinutes: 10 },
      },
    },
    {
      key: "high",
      name: "High",
      description: "High glucose alert for sustained elevated readings",
      icon: TrendingUp,
      conditionType: "threshold_high",
      conditionParams: { threshold: 250, direction: "above" },
      threshold: 250,
      thresholdUnit: "mg/dL",
      thresholdField: "threshold",
      confirmationReadings: 3,
      hysteresisMinutes: 30,
      enabled: false,
      severity: "normal",
      clientConfiguration: {
        audio: { enabled: true, sound: "alarm-high", ascending: false, startVolume: 60, maxVolume: 60, ascendDurationSeconds: 0, repeatCount: 2 },
        visual: { flashEnabled: false, flashColor: "#ff0000", persistentBanner: true, wakeScreen: false },
        snooze: { defaultMinutes: 30, options: [15, 30, 60], maxCount: 5, smartSnooze: false, smartSnoozeExtendMinutes: 10 },
      },
    },
    {
      key: "urgent_high",
      name: "Urgent High",
      description: "Critical high glucose alert requiring prompt action",
      icon: AlertTriangle,
      conditionType: "threshold_high",
      conditionParams: { threshold: 300, direction: "above" },
      threshold: 300,
      thresholdUnit: "mg/dL",
      thresholdField: "threshold",
      confirmationReadings: 2,
      hysteresisMinutes: 30,
      enabled: false,
      severity: "critical",
      clientConfiguration: {
        audio: { enabled: true, sound: "alarm-urgent", ascending: true, startVolume: 50, maxVolume: 100, ascendDurationSeconds: 30, repeatCount: 3 },
        visual: { flashEnabled: true, flashColor: "#ff0000", persistentBanner: true, wakeScreen: true },
        snooze: { defaultMinutes: 15, options: [5, 15, 30], maxCount: 3, smartSnooze: false, smartSnoozeExtendMinutes: 10 },
      },
    },
    {
      key: "fast_drop",
      name: "Fast Drop",
      description: "Rapid glucose decline combined with low threshold",
      icon: Zap,
      conditionType: "composite",
      conditionParams: {
        conditions: [
          { type: "threshold_low", threshold: 100 },
          { type: "rate_of_change", rateThreshold: 3.0, direction: "falling" },
        ],
      },
      threshold: 100,
      thresholdUnit: "mg/dL",
      thresholdField: "threshold",
      confirmationReadings: 2,
      hysteresisMinutes: 15,
      enabled: false,
      severity: "normal",
      clientConfiguration: {
        audio: { enabled: true, sound: "alert", ascending: true, startVolume: 40, maxVolume: 90, ascendDurationSeconds: 30, repeatCount: 2 },
        visual: { flashEnabled: false, flashColor: "#ff0000", persistentBanner: true, wakeScreen: false },
        snooze: { defaultMinutes: 15, options: [5, 15, 30], maxCount: 5, smartSnooze: true, smartSnoozeExtendMinutes: 10 },
      },
    },
    {
      key: "sensor_lost",
      name: "Sensor Lost",
      description: "Alert when CGM signal is lost for an extended period",
      icon: WifiOff,
      conditionType: "signal_loss",
      conditionParams: { minutes: 15 },
      threshold: 15,
      thresholdUnit: "minutes",
      thresholdField: "minutes",
      confirmationReadings: 1,
      hysteresisMinutes: 5,
      enabled: false,
      severity: "normal",
      clientConfiguration: {
        audio: { enabled: true, sound: "chime", ascending: false, startVolume: 50, maxVolume: 50, ascendDurationSeconds: 0, repeatCount: 1 },
        visual: { flashEnabled: false, flashColor: "#ff0000", persistentBanner: true, wakeScreen: false },
        snooze: { defaultMinutes: 30, options: [15, 30, 60], maxCount: 5, smartSnooze: false, smartSnoozeExtendMinutes: 10 },
      },
    },
  ]);

  // Step 2: Delivery channels
  let webPushEnabled = $state(false);
  let webhookEnabled = $state(false);
  let webhookUrl = $state("");

  // Step 3: Saving state
  let saving = $state(false);
  let saveError = $state<string | null>(null);

  const selectedPresets = $derived(presets.filter((p) => p.enabled));

  function updateThreshold(key: string, value: number) {
    const preset = presets.find((p) => p.key === key);
    if (!preset) return;
    preset.threshold = value;

    if (preset.conditionType === "composite") {
      const conditions = preset.conditionParams.conditions as Array<Record<string, unknown>>;
      if (conditions?.[0]) {
        conditions[0].threshold = value;
      }
    } else if (preset.conditionType === "signal_loss") {
      preset.conditionParams.minutes = value;
    } else {
      preset.conditionParams.threshold = value;
    }
  }

  function togglePreset(key: string) {
    const preset = presets.find((p) => p.key === key);
    if (preset) {
      preset.enabled = !preset.enabled;
    }
  }

  async function handleSave() {
    saving = true;
    saveError = null;

    try {
      for (const preset of selectedPresets) {
        const channels: Array<{
          channelType: string;
          destination: string;
          destinationLabel?: string;
        }> = [];

        if (webPushEnabled) {
          channels.push({
            channelType: "web_push",
            destination: "browser",
            destinationLabel: "Browser Push Notification",
          });
        }
        if (webhookEnabled && webhookUrl) {
          channels.push({
            channelType: "webhook",
            destination: webhookUrl,
            destinationLabel: "Webhook",
          });
        }

        const request: CreateAlertRuleRequest = {
          name: preset.name,
          description: preset.description,
          conditionType: preset.conditionType,
          conditionParams: preset.conditionParams,
          hysteresisMinutes: preset.hysteresisMinutes,
          confirmationReadings: preset.confirmationReadings,
          isEnabled: true,
          sortOrder: presets.indexOf(preset),
          severity: preset.severity,
          clientConfiguration: preset.clientConfiguration,
          schedules: [
            {
              name: "Default",
              isDefault: true,
              escalationSteps:
                channels.length > 0
                  ? [
                      {
                        stepOrder: 0,
                        delaySeconds: 0,
                        channels,
                      },
                    ]
                  : undefined,
            },
          ],
        };

        await createRule(request);
      }

      goto("/settings/alerts");
    } catch (err) {
      saveError =
        err instanceof Error ? err.message : "Failed to create alert rules";
    } finally {
      saving = false;
    }
  }
</script>

<svelte:head>
  <title>Alert Setup - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto max-w-3xl p-6 space-y-6">
  <!-- Header -->
  <div>
    <Button
      variant="ghost"
      size="sm"
      class="mb-2"
      onclick={() => goto("/settings/alerts")}
    >
      <ArrowLeft class="h-4 w-4 mr-2" />
      Back to Alerts
    </Button>
    <h1 class="text-2xl font-bold tracking-tight">Alert Setup Wizard</h1>
    <p class="text-muted-foreground">
      Configure your glucose alert rules in a few simple steps
    </p>
  </div>

  <!-- Step Indicator -->
  <div class="flex items-center gap-2">
    {#each Array(totalSteps) as _, i}
      {@const step = i + 1}
      <div class="flex items-center gap-2 flex-1">
        <div
          class="flex items-center justify-center h-8 w-8 rounded-full text-sm font-medium shrink-0 {step <= currentStep
            ? 'bg-primary text-primary-foreground'
            : 'bg-muted text-muted-foreground'}"
        >
          {#if step < currentStep}
            <Check class="h-4 w-4" />
          {:else}
            {step}
          {/if}
        </div>
        <span
          class="text-sm hidden sm:inline {step === currentStep
            ? 'font-medium'
            : 'text-muted-foreground'}"
        >
          {#if step === 1}
            Choose Presets
          {:else if step === 2}
            Delivery Channels
          {:else}
            Review & Save
          {/if}
        </span>
        {#if i < totalSteps - 1}
          <div
            class="flex-1 h-px {step < currentStep
              ? 'bg-primary'
              : 'bg-muted'}"
          ></div>
        {/if}
      </div>
    {/each}
  </div>

  <!-- Step 1: Choose Presets -->
  {#if currentStep === 1}
    <div class="space-y-4">
      <div>
        <h2 class="text-lg font-semibold">Choose Alert Presets</h2>
        <p class="text-sm text-muted-foreground">
          Select the alerts you want to enable. You can customize thresholds for
          each one.
        </p>
      </div>

      <div class="grid gap-3 sm:grid-cols-2">
        {#each presets as preset (preset.key)}
          {@const PresetIcon = preset.icon}
          <Card
            class="cursor-pointer transition-all {preset.enabled
              ? 'border-primary ring-1 ring-primary/20'
              : 'hover:border-primary/50'}"
          >
            <CardContent class="p-4">
              <button
                class="flex items-start gap-3 w-full text-left"
                onclick={() => togglePreset(preset.key)}
              >
                <div
                  class="flex items-center justify-center h-10 w-10 rounded-lg shrink-0 {preset.enabled
                    ? 'bg-primary/10 text-primary'
                    : 'bg-muted text-muted-foreground'}"
                >
                  <PresetIcon class="h-5 w-5" />
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center justify-between mb-1">
                    <span class="font-medium">{preset.name}</span>
                    <Switch
                      checked={preset.enabled}
                      onCheckedChange={() => togglePreset(preset.key)}
                    />
                  </div>
                  <p class="text-xs text-muted-foreground">
                    {preset.description}
                  </p>
                </div>
              </button>

              {#if preset.enabled}
                <div class="mt-3 pt-3 border-t space-y-2">
                  <div class="flex items-center gap-2">
                    <Label class="text-xs w-20 shrink-0">Threshold</Label>
                    <Input
                      type="number"
                      value={preset.threshold}
                      class="h-8 text-sm"
                      oninput={(e) =>
                        updateThreshold(
                          preset.key,
                          parseFloat(e.currentTarget.value) || 0,
                        )}
                    />
                    <span class="text-xs text-muted-foreground shrink-0">
                      {preset.thresholdUnit}
                    </span>
                  </div>
                  <div
                    class="flex items-center gap-4 text-xs text-muted-foreground"
                  >
                    <span>
                      {preset.confirmationReadings} confirmation{preset.confirmationReadings !== 1 ? "s" : ""}
                    </span>
                    <span>{preset.hysteresisMinutes}m hysteresis</span>
                  </div>
                </div>
              {/if}
            </CardContent>
          </Card>
        {/each}
      </div>
    </div>
  {/if}

  <!-- Step 2: Delivery Channels -->
  {#if currentStep === 2}
    <div class="space-y-4">
      <div>
        <h2 class="text-lg font-semibold">Delivery Channels</h2>
        <p class="text-sm text-muted-foreground">
          Choose how you want to receive alert notifications.
        </p>
      </div>

      <Card>
        <CardContent class="p-4 space-y-4">
          <!-- Web Push -->
          <div class="flex items-center justify-between p-3 rounded-lg border">
            <div class="flex items-center gap-3">
              <div
                class="flex items-center justify-center h-10 w-10 rounded-lg bg-primary/10"
              >
                <Bell class="h-5 w-5 text-primary" />
              </div>
              <div>
                <Label>Browser Push Notifications</Label>
                <p class="text-sm text-muted-foreground">
                  Receive alerts directly in your browser
                </p>
              </div>
            </div>
            <Switch
              checked={webPushEnabled}
              onCheckedChange={(checked) => (webPushEnabled = checked)}
            />
          </div>

          <!-- Webhook -->
          <div class="p-3 rounded-lg border space-y-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-3">
                <div
                  class="flex items-center justify-center h-10 w-10 rounded-lg bg-primary/10"
                >
                  <Webhook class="h-5 w-5 text-primary" />
                </div>
                <div>
                  <Label>Webhook</Label>
                  <p class="text-sm text-muted-foreground">
                    Send alert data to a custom URL
                  </p>
                </div>
              </div>
              <Switch
                checked={webhookEnabled}
                onCheckedChange={(checked) => (webhookEnabled = checked)}
              />
            </div>
            {#if webhookEnabled}
              <div class="pl-13">
                <Label class="text-xs">Webhook URL</Label>
                <Input
                  placeholder="https://example.com/webhook"
                  value={webhookUrl}
                  oninput={(e) => (webhookUrl = e.currentTarget.value)}
                  class="mt-1"
                />
              </div>
            {/if}
          </div>

          <Separator />

          <!-- Coming Soon Channels -->
          <p class="text-sm font-medium text-muted-foreground">Coming Soon</p>

          <div
            class="flex items-center justify-between p-3 rounded-lg border opacity-50"
          >
            <div class="flex items-center gap-3">
              <div
                class="flex items-center justify-center h-10 w-10 rounded-lg bg-muted"
              >
                <MessageSquare class="h-5 w-5 text-muted-foreground" />
              </div>
              <div>
                <Label class="text-muted-foreground">Discord</Label>
                <p class="text-sm text-muted-foreground">
                  Send alerts to a Discord channel
                </p>
              </div>
            </div>
            <Badge variant="secondary">Coming Soon</Badge>
          </div>

          <div
            class="flex items-center justify-between p-3 rounded-lg border opacity-50"
          >
            <div class="flex items-center gap-3">
              <div
                class="flex items-center justify-center h-10 w-10 rounded-lg bg-muted"
              >
                <Send class="h-5 w-5 text-muted-foreground" />
              </div>
              <div>
                <Label class="text-muted-foreground">Telegram</Label>
                <p class="text-sm text-muted-foreground">
                  Send alerts to a Telegram chat
                </p>
              </div>
            </div>
            <Badge variant="secondary">Coming Soon</Badge>
          </div>
        </CardContent>
      </Card>
    </div>
  {/if}

  <!-- Step 3: Review & Save -->
  {#if currentStep === 3}
    <div class="space-y-4">
      <div>
        <h2 class="text-lg font-semibold">Review & Save</h2>
        <p class="text-sm text-muted-foreground">
          Review your alert configuration before saving.
        </p>
      </div>

      <!-- Selected Rules Summary -->
      <Card>
        <CardHeader>
          <CardTitle class="text-base">
            Selected Alert Rules ({selectedPresets.length})
          </CardTitle>
        </CardHeader>
        <CardContent class="space-y-2">
          {#if selectedPresets.length === 0}
            <p class="text-sm text-muted-foreground py-4 text-center">
              No presets selected. Go back to step 1 to select at least one
              alert.
            </p>
          {:else}
            {#each selectedPresets as preset (preset.key)}
              {@const PresetIcon = preset.icon}
              <div class="flex items-center gap-3 p-3 rounded-lg border">
                <PresetIcon class="h-4 w-4 text-primary shrink-0" />
                <div class="flex-1 min-w-0">
                  <span class="text-sm font-medium">{preset.name}</span>
                  {#if preset.severity === "critical"}
                    <Badge variant="destructive" class="ml-2 text-xs">Critical</Badge>
                  {/if}
                  <span class="text-xs text-muted-foreground ml-2">
                    {preset.threshold}
                    {preset.thresholdUnit}
                  </span>
                </div>
                <div class="text-xs text-muted-foreground">
                  {preset.confirmationReadings} conf. / {preset.hysteresisMinutes}m hyst.
                </div>
              </div>
            {/each}
          {/if}
        </CardContent>
      </Card>

      <!-- Channels Summary -->
      <Card>
        <CardHeader>
          <CardTitle class="text-base">Delivery Channels</CardTitle>
        </CardHeader>
        <CardContent class="space-y-2">
          {#if !webPushEnabled && !webhookEnabled}
            <p class="text-sm text-muted-foreground py-2">
              No delivery channels configured. Alerts will still be visible in
              the dashboard, but no push notifications will be sent.
            </p>
          {:else}
            {#if webPushEnabled}
              <div class="flex items-center gap-2 text-sm">
                <Bell class="h-4 w-4 text-primary" />
                <span>Browser Push Notifications</span>
              </div>
            {/if}
            {#if webhookEnabled && webhookUrl}
              <div class="flex items-center gap-2 text-sm">
                <Webhook class="h-4 w-4 text-primary" />
                <span>Webhook: {webhookUrl}</span>
              </div>
            {/if}
          {/if}
        </CardContent>
      </Card>

      <!-- Disclaimer -->
      <Card class="border-amber-500/30 bg-amber-500/5">
        <CardContent class="flex gap-3 pt-6">
          <Shield class="h-5 w-5 text-amber-600 shrink-0 mt-0.5" />
          <div class="text-sm">
            <p class="font-medium text-amber-800 dark:text-amber-200 mb-1">
              Medical Disclaimer
            </p>
            <p class="text-muted-foreground">
              Nocturne alerts are not a substitute for professional medical
              advice, diagnosis, or treatment. Always consult your healthcare
              provider for medical decisions. Alert delivery depends on network
              connectivity, device availability, and third-party service
              reliability. Do not rely solely on these alerts for critical
              medical decisions.
            </p>
          </div>
        </CardContent>
      </Card>

      {#if saveError}
        <Card class="border-destructive">
          <CardContent class="flex items-center gap-3 pt-6">
            <AlertTriangle class="h-5 w-5 text-destructive" />
            <p class="text-sm text-destructive">{saveError}</p>
          </CardContent>
        </Card>
      {/if}
    </div>
  {/if}

  <!-- Navigation Buttons -->
  <div class="flex items-center justify-between pt-4 border-t">
    <Button
      variant="outline"
      onclick={() => {
        if (currentStep === 1) {
          goto("/settings/alerts");
        } else {
          currentStep--;
        }
      }}
    >
      <ArrowLeft class="h-4 w-4 mr-2" />
      {currentStep === 1 ? "Cancel" : "Previous"}
    </Button>

    {#if currentStep < totalSteps}
      <Button onclick={() => currentStep++}>
        Next
        <ArrowRight class="h-4 w-4 ml-2" />
      </Button>
    {:else}
      <Button
        onclick={handleSave}
        disabled={saving || selectedPresets.length === 0}
      >
        {#if saving}
          <Loader2 class="h-4 w-4 mr-2 animate-spin" />
          Creating Rules...
        {:else}
          <Check class="h-4 w-4 mr-2" />
          Create {selectedPresets.length} Rule{selectedPresets.length !== 1 ? "s" : ""}
        {/if}
      </Button>
    {/if}
  </div>
</div>
