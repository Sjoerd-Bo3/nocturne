<script lang="ts">
  import { onMount } from "svelte";
  import {
    getRules,
    deleteRule,
    toggleRule,
  } from "$api/generated/alertRules.generated.remote";
  import {
    getActiveAlerts,
    getAlertHistory,
    acknowledge,
    getQuietHours,
    updateQuietHours,
  } from "$api/generated/alerts.generated.remote";
  import type {
    AlertRuleResponse,
    ActiveExcursionResponse,
    AlertHistoryResponse,
  } from "$api-clients";
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import { Switch } from "$lib/components/ui/switch";
  import { Label } from "$lib/components/ui/label";
  import { Input } from "$lib/components/ui/input";
  import { Separator } from "$lib/components/ui/separator";
  import * as AlertDialog from "$lib/components/ui/alert-dialog";
  import SettingsPageSkeleton from "$lib/components/settings/SettingsPageSkeleton.svelte";
  import {
    Bell,
    Plus,
    Trash2,
    AlertTriangle,
    Check,
    ChevronDown,
    ChevronUp,
    Clock,
    Loader2,
    Shield,
    Zap,
    WifiOff,
    TrendingDown,
    TrendingUp,
    ArrowUpRight,
    Moon,
    Save,
  } from "lucide-svelte";
  import { goto } from "$app/navigation";
  import RuleEditorSheet from "$lib/components/alerts/RuleEditorSheet.svelte";
  import { Pencil } from "lucide-svelte";

  let rules = $state<AlertRuleResponse[]>([]);
  let activeAlerts = $state<ActiveExcursionResponse[]>([]);
  let history = $state<AlertHistoryResponse | null>(null);
  let loading = $state(true);
  let error = $state<string | null>(null);
  let expandedRuleId = $state<string | null>(null);
  let deletingRuleId = $state<string | null>(null);
  let togglingRuleId = $state<string | null>(null);
  let acknowledging = $state(false);
  let historyPage = $state(1);
  let historyLoading = $state(false);
  let editorOpen = $state(false);
  let editingRule = $state<AlertRuleResponse | null>(null);

  // Quiet hours
  let quietHoursEnabled = $state(false);
  let quietHoursStart = $state("22:00");
  let quietHoursEnd = $state("07:00");
  let quietHoursOverrideCritical = $state(true);
  let quietHoursSaving = $state(false);

  function getConditionIcon(conditionType: string | undefined) {
    switch (conditionType) {
      case "threshold_low":
        return TrendingDown;
      case "threshold_high":
        return TrendingUp;
      case "rate_of_change":
        return Zap;
      case "signal_loss":
        return WifiOff;
      case "composite":
        return Shield;
      default:
        return Bell;
    }
  }

  function getConditionBadgeVariant(
    conditionType: string | undefined,
  ): "default" | "secondary" | "destructive" | "outline" {
    switch (conditionType) {
      case "threshold_low":
        return "destructive";
      case "threshold_high":
        return "default";
      case "signal_loss":
        return "secondary";
      default:
        return "outline";
    }
  }

  function getConditionLabel(conditionType: string | undefined): string {
    switch (conditionType) {
      case "threshold_low":
        return "Low Threshold";
      case "threshold_high":
        return "High Threshold";
      case "rate_of_change":
        return "Rate of Change";
      case "signal_loss":
        return "Signal Loss";
      case "composite":
        return "Composite";
      default:
        return conditionType ?? "Unknown";
    }
  }

  function getConditionSummary(rule: AlertRuleResponse): string {
    const params = rule.conditionParams;
    if (!params) return "No condition configured";

    switch (rule.conditionType) {
      case "threshold_low":
        return `Below ${params.threshold ?? "?"} mg/dL`;
      case "threshold_high":
        return `Above ${params.threshold ?? "?"} mg/dL`;
      case "rate_of_change": {
        const dir = params.direction === "falling" ? "Falling" : "Rising";
        return `${dir} faster than ${params.rateThreshold ?? "?"} mg/dL/min`;
      }
      case "signal_loss":
        return `No data for ${params.minutes ?? "?"} minutes`;
      case "composite":
        return "Multiple conditions combined";
      default:
        return "Custom condition";
    }
  }

  function formatDate(date: Date | string | undefined): string {
    if (!date) return "-";
    const d = typeof date === "string" ? new Date(date) : date;
    return d.toLocaleString(undefined, {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  function formatDuration(
    start: Date | string | undefined,
    end: Date | string | undefined,
  ): string {
    if (!start || !end) return "-";
    const s = typeof start === "string" ? new Date(start) : start;
    const e = typeof end === "string" ? new Date(end) : end;
    const diffMin = Math.round((e.getTime() - s.getTime()) / 60000);
    if (diffMin < 60) return `${diffMin}m`;
    const h = Math.floor(diffMin / 60);
    const m = diffMin % 60;
    return `${h}h ${m}m`;
  }

  async function loadData() {
    loading = true;
    error = null;
    try {
      const [rulesResult, alertsResult, historyResult, qhResult] = await Promise.all([
        getRules(),
        getActiveAlerts(),
        getAlertHistory({ page: 1, pageSize: 10 }),
        getQuietHours().catch(() => null),
      ]);
      rules = Array.isArray(rulesResult) ? rulesResult : [];
      activeAlerts = Array.isArray(alertsResult) ? alertsResult : [];
      history = historyResult ?? null;
      if (qhResult) {
        quietHoursEnabled = qhResult.enabled ?? false;
        quietHoursStart = qhResult.startTime ?? "22:00";
        quietHoursEnd = qhResult.endTime ?? "07:00";
        quietHoursOverrideCritical = qhResult.overrideCritical ?? true;
      }
    } catch (err) {
      error =
        err instanceof Error ? err.message : "Failed to load alert settings";
    } finally {
      loading = false;
    }
  }

  async function loadHistory(page: number) {
    historyLoading = true;
    try {
      const result = await getAlertHistory({ page, pageSize: 10 });
      history = result ?? null;
      historyPage = page;
    } catch {
      // Keep existing history on error
    } finally {
      historyLoading = false;
    }
  }

  async function handleToggleRule(ruleId: string) {
    togglingRuleId = ruleId;
    try {
      await toggleRule(ruleId);
      const result = await getRules();
      rules = Array.isArray(result) ? result : [];
    } catch {
      // Error handled by remote function
    } finally {
      togglingRuleId = null;
    }
  }

  async function handleDeleteRule(ruleId: string) {
    deletingRuleId = ruleId;
    try {
      await deleteRule(ruleId);
      const result = await getRules();
      rules = Array.isArray(result) ? result : [];
    } catch {
      // Error handled by remote function
    } finally {
      deletingRuleId = null;
    }
  }

  async function handleAcknowledge() {
    acknowledging = true;
    try {
      await acknowledge({ acknowledgedBy: "web_user" });
      const result = await getActiveAlerts();
      activeAlerts = Array.isArray(result) ? result : [];
    } catch {
      // Error handled by remote function
    } finally {
      acknowledging = false;
    }
  }

  function toggleExpand(ruleId: string) {
    expandedRuleId = expandedRuleId === ruleId ? null : ruleId;
  }

  async function handleSaveQuietHours() {
    quietHoursSaving = true;
    try {
      await updateQuietHours({
        enabled: quietHoursEnabled,
        startTime: quietHoursEnabled ? quietHoursStart : undefined,
        endTime: quietHoursEnabled ? quietHoursEnd : undefined,
        overrideCritical: quietHoursOverrideCritical,
      });
    } catch {
      // Error handled by remote function
    } finally {
      quietHoursSaving = false;
    }
  }

  function openCreateEditor() {
    editingRule = null;
    editorOpen = true;
  }

  function openEditEditor(rule: AlertRuleResponse) {
    editingRule = rule;
    editorOpen = true;
  }

  async function handleEditorSave() {
    const result = await getRules();
    rules = Array.isArray(result) ? result : [];
  }

  onMount(() => {
    loadData();
  });
</script>

<svelte:head>
  <title>Alerts - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto max-w-4xl p-6 space-y-6">
  <!-- Header -->
  <div class="flex items-center justify-between">
    <div>
      <h1 class="text-2xl font-bold tracking-tight">Alerts</h1>
      <p class="text-muted-foreground">
        Configure alert rules, schedules, and escalation chains
      </p>
    </div>
    <div class="flex items-center gap-2">
      <Button variant="outline" onclick={() => goto("/settings/alerts/setup")}>
        <Zap class="h-4 w-4 mr-2" />
        Setup Wizard
      </Button>
      <Button onclick={openCreateEditor}>
        <Plus class="h-4 w-4 mr-2" />
        Add Rule
      </Button>
    </div>
  </div>

  {#if loading}
    <SettingsPageSkeleton cardCount={4} />
  {:else if error}
    <Card class="border-destructive">
      <CardContent class="flex items-center gap-3 pt-6">
        <AlertTriangle class="h-5 w-5 text-destructive" />
        <div>
          <p class="font-medium">Failed to load alert settings</p>
          <p class="text-sm text-muted-foreground">{error}</p>
        </div>
      </CardContent>
    </Card>
  {:else}
    <!-- Active Alerts Banner -->
    {#if activeAlerts.length > 0}
      <Card class="border-destructive/50 bg-destructive/5">
        <CardHeader class="pb-3">
          <div class="flex items-center justify-between">
            <CardTitle class="flex items-center gap-2 text-destructive">
              <AlertTriangle class="h-5 w-5" />
              Active Alerts ({activeAlerts.length})
            </CardTitle>
            <Button
              variant="outline"
              size="sm"
              onclick={handleAcknowledge}
              disabled={acknowledging}
            >
              {#if acknowledging}
                <Loader2 class="h-4 w-4 mr-2 animate-spin" />
              {:else}
                <Check class="h-4 w-4 mr-2" />
              {/if}
              Acknowledge All
            </Button>
          </div>
        </CardHeader>
        <CardContent class="space-y-2">
          {#each activeAlerts as alert (alert.id)}
            <div
              class="flex items-center gap-3 p-3 rounded-lg bg-background border"
            >
              <AlertTriangle class="h-4 w-4 text-destructive shrink-0" />
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium">
                  {alert.ruleName ?? "Alert"}
                </p>
                <p class="text-xs text-muted-foreground">
                  {getConditionLabel(alert.conditionType)} — Started {formatDate(alert.startedAt)}
                </p>
              </div>
              {#if alert.acknowledgedAt}
                <Badge variant="secondary">Acknowledged</Badge>
              {/if}
            </div>
          {/each}
        </CardContent>
      </Card>
    {/if}

    <!-- Alert Rules -->
    <Card>
      <CardHeader>
        <div class="flex items-center justify-between">
          <div>
            <CardTitle class="flex items-center gap-2">
              <Bell class="h-5 w-5" />
              Alert Rules
            </CardTitle>
            <CardDescription>
              Rules define when alerts trigger and how they escalate
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="space-y-3">
        {#if rules.length === 0}
          <div class="text-center py-12 text-muted-foreground">
            <Bell class="h-12 w-12 mx-auto mb-4 opacity-50" />
            <p class="font-medium">No alert rules configured</p>
            <p class="text-sm mb-4">
              Use the setup wizard to create your first alert rules
            </p>
            <Button
              variant="outline"
              onclick={() => goto("/settings/alerts/setup")}
            >
              <Plus class="h-4 w-4 mr-2" />
              Setup Wizard
            </Button>
          </div>
        {:else}
          {#each rules as rule (rule.id)}
            {@const ConditionIcon = getConditionIcon(rule.conditionType)}
            <div class="rounded-lg border transition-all hover:shadow-sm">
              <!-- Rule Summary Row -->
              <button
                class="flex items-center gap-4 p-4 w-full text-left"
                onclick={() => toggleExpand(rule.id ?? "")}
              >
                <ConditionIcon
                  class="h-5 w-5 shrink-0 {rule.isEnabled
                    ? 'text-primary'
                    : 'text-muted-foreground'}"
                />
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 mb-1">
                    <span class="font-medium truncate">
                      {rule.name ?? "Unnamed Rule"}
                    </span>
                    <Badge variant={getConditionBadgeVariant(rule.conditionType)}>
                      {getConditionLabel(rule.conditionType)}
                    </Badge>
                    {#if !rule.isEnabled}
                      <Badge variant="secondary">Disabled</Badge>
                    {/if}
                  </div>
                  <div
                    class="flex items-center gap-4 text-sm text-muted-foreground"
                  >
                    <span>{getConditionSummary(rule)}</span>
                    {#if rule.schedules && rule.schedules.length > 0}
                      <span class="flex items-center gap-1">
                        <Clock class="h-3 w-3" />
                        {rule.schedules.length} schedule{rule.schedules.length !== 1 ? "s" : ""}
                      </span>
                    {/if}
                    {#if rule.schedules}
                      {@const stepCount = rule.schedules.reduce(
                        (acc, s) => acc + (s.escalationSteps?.length ?? 0),
                        0,
                      )}
                      {#if stepCount > 0}
                        <span class="flex items-center gap-1">
                          <ArrowUpRight class="h-3 w-3" />
                          {stepCount} escalation step{stepCount !== 1 ? "s" : ""}
                        </span>
                      {/if}
                    {/if}
                    {#if rule.confirmationReadings && rule.confirmationReadings > 1}
                      <span>{rule.confirmationReadings} confirmations</span>
                    {/if}
                  </div>
                </div>
                <div class="flex items-center gap-2 shrink-0">
                  <Switch
                    checked={rule.isEnabled ?? false}
                    onCheckedChange={() => handleToggleRule(rule.id ?? "")}
                    disabled={togglingRuleId === rule.id}
                    onclick={(e: MouseEvent) => e.stopPropagation()}
                  />
                  {#if expandedRuleId === rule.id}
                    <ChevronUp class="h-4 w-4 text-muted-foreground" />
                  {:else}
                    <ChevronDown class="h-4 w-4 text-muted-foreground" />
                  {/if}
                </div>
              </button>

              <!-- Expanded Detail -->
              {#if expandedRuleId === rule.id}
                <div class="border-t px-4 py-4 space-y-4 bg-muted/30">
                  {#if rule.description}
                    <p class="text-sm text-muted-foreground">
                      {rule.description}
                    </p>
                  {/if}

                  <div class="grid gap-4 sm:grid-cols-3 text-sm">
                    <div>
                      <p class="text-muted-foreground mb-1">Hysteresis</p>
                      <p class="font-medium">
                        {rule.hysteresisMinutes ?? 0} minutes
                      </p>
                    </div>
                    <div>
                      <p class="text-muted-foreground mb-1">Confirmations</p>
                      <p class="font-medium">
                        {rule.confirmationReadings ?? 1} reading{(rule.confirmationReadings ?? 1) !== 1 ? "s" : ""}
                      </p>
                    </div>
                    <div>
                      <p class="text-muted-foreground mb-1">Sort Order</p>
                      <p class="font-medium">{rule.sortOrder ?? 0}</p>
                    </div>
                  </div>

                  <!-- Schedules -->
                  {#if rule.schedules && rule.schedules.length > 0}
                    <Separator />
                    <div>
                      <h4 class="text-sm font-medium mb-3">Schedules</h4>
                      {#each rule.schedules as schedule (schedule.id)}
                        <div class="mb-3 p-3 rounded-md border bg-background">
                          <div class="flex items-center gap-2 mb-2">
                            <Clock class="h-4 w-4 text-muted-foreground" />
                            <span class="text-sm font-medium">
                              {schedule.name ?? "Default Schedule"}
                            </span>
                            {#if schedule.isDefault}
                              <Badge variant="secondary">Default</Badge>
                            {/if}
                          </div>
                          {#if schedule.startTime || schedule.endTime}
                            <p class="text-xs text-muted-foreground mb-2">
                              {schedule.startTime ?? "00:00"} - {schedule.endTime ?? "23:59"}
                              ({schedule.timezone ?? "UTC"})
                            </p>
                          {/if}
                          {#if schedule.daysOfWeek && schedule.daysOfWeek.length > 0 && schedule.daysOfWeek.length < 7}
                            <p class="text-xs text-muted-foreground mb-2">
                              Days: {schedule.daysOfWeek.map((d) => ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"][d] ?? d).join(", ")}
                            </p>
                          {/if}

                          <!-- Escalation Steps -->
                          {#if schedule.escalationSteps && schedule.escalationSteps.length > 0}
                            <div class="mt-2 space-y-1">
                              {#each schedule.escalationSteps.sort((a, b) => (a.stepOrder ?? 0) - (b.stepOrder ?? 0)) as step, idx}
                                <div
                                  class="flex items-center gap-2 text-xs text-muted-foreground pl-4 border-l-2 border-muted py-1"
                                >
                                  <span class="font-medium text-foreground">
                                    Step {idx + 1}
                                  </span>
                                  {#if step.delaySeconds && step.delaySeconds > 0}
                                    <span>
                                      after {Math.round(step.delaySeconds / 60)}m
                                    </span>
                                  {:else}
                                    <span>immediately</span>
                                  {/if}
                                  {#if step.channels && step.channels.length > 0}
                                    <span class="mx-1">via</span>
                                    {#each step.channels as channel}
                                      <Badge variant="outline" class="text-xs">
                                        {channel.channelType}
                                        {#if channel.destinationLabel}
                                          : {channel.destinationLabel}
                                        {/if}
                                      </Badge>
                                    {/each}
                                  {/if}
                                </div>
                              {/each}
                            </div>
                          {/if}
                        </div>
                      {/each}
                    </div>
                  {/if}

                  <!-- Actions -->
                  <Separator />
                  <div class="flex items-center justify-end gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onclick={() => openEditEditor(rule)}
                    >
                      <Pencil class="h-4 w-4 mr-2" />
                      Edit Rule
                    </Button>
                    <AlertDialog.Root>
                      <AlertDialog.Trigger>
                        {#snippet child({ props })}
                          <Button
                            {...props}
                            variant="outline"
                            size="sm"
                            class="text-destructive"
                          >
                            <Trash2 class="h-4 w-4 mr-2" />
                            Delete Rule
                          </Button>
                        {/snippet}
                      </AlertDialog.Trigger>
                      <AlertDialog.Content>
                        <AlertDialog.Header>
                          <AlertDialog.Title>Delete Alert Rule</AlertDialog.Title>
                          <AlertDialog.Description>
                            Are you sure you want to delete "{rule.name}"? This
                            will also remove all associated schedules and
                            escalation steps. This action cannot be undone.
                          </AlertDialog.Description>
                        </AlertDialog.Header>
                        <AlertDialog.Footer>
                          <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                          <AlertDialog.Action
                            onclick={() => handleDeleteRule(rule.id ?? "")}
                          >
                            {#if deletingRuleId === rule.id}
                              <Loader2 class="h-4 w-4 mr-2 animate-spin" />
                            {/if}
                            Delete
                          </AlertDialog.Action>
                        </AlertDialog.Footer>
                      </AlertDialog.Content>
                    </AlertDialog.Root>
                  </div>
                </div>
              {/if}
            </div>
          {/each}
        {/if}
      </CardContent>
    </Card>

    <!-- Quiet Hours -->
    <Card>
      <CardHeader>
        <CardTitle class="flex items-center gap-2">
          <Moon class="h-5 w-5" />
          Quiet Hours
        </CardTitle>
        <CardDescription>
          Suppress non-critical alerts during specific hours
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-4">
        <div class="flex items-center justify-between">
          <Label for="qh-enabled">Enable quiet hours</Label>
          <Switch id="qh-enabled" bind:checked={quietHoursEnabled} />
        </div>

        {#if quietHoursEnabled}
          <div class="grid grid-cols-2 gap-4">
            <div class="space-y-2">
              <Label for="qh-start">Start Time</Label>
              <Input id="qh-start" type="time" bind:value={quietHoursStart} />
            </div>
            <div class="space-y-2">
              <Label for="qh-end">End Time</Label>
              <Input id="qh-end" type="time" bind:value={quietHoursEnd} />
            </div>
          </div>

          <div class="flex items-center justify-between">
            <div>
              <Label for="qh-override">Allow critical alerts during quiet hours</Label>
              <p class="text-xs text-muted-foreground">
                Critical alerts bypass quiet hours
              </p>
            </div>
            <Switch id="qh-override" bind:checked={quietHoursOverrideCritical} />
          </div>
        {/if}

        <div class="flex justify-end">
          <Button
            size="sm"
            onclick={handleSaveQuietHours}
            disabled={quietHoursSaving}
          >
            {#if quietHoursSaving}
              <Loader2 class="h-4 w-4 mr-2 animate-spin" />
            {:else}
              <Save class="h-4 w-4 mr-2" />
            {/if}
            Save
          </Button>
        </div>
      </CardContent>
    </Card>

    <!-- Alert History -->
    <Card>
      <CardHeader>
        <CardTitle class="flex items-center gap-2">
          <Clock class="h-5 w-5" />
          Alert History
        </CardTitle>
        <CardDescription>
          Past alert excursions and their resolution
        </CardDescription>
      </CardHeader>
      <CardContent>
        {#if !history || !history.items || history.items.length === 0}
          <div class="text-center py-8 text-muted-foreground">
            <Clock class="h-12 w-12 mx-auto mb-4 opacity-50" />
            <p class="font-medium">No alert history</p>
            <p class="text-sm">
              Resolved alerts will appear here
            </p>
          </div>
        {:else}
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b">
                  <th class="text-left py-2 pr-4 font-medium text-muted-foreground">Rule</th>
                  <th class="text-left py-2 pr-4 font-medium text-muted-foreground">Type</th>
                  <th class="text-left py-2 pr-4 font-medium text-muted-foreground">Started</th>
                  <th class="text-left py-2 pr-4 font-medium text-muted-foreground">Duration</th>
                  <th class="text-left py-2 font-medium text-muted-foreground">Acknowledged</th>
                </tr>
              </thead>
              <tbody>
                {#each history.items as item (item.id)}
                  <tr class="border-b last:border-0">
                    <td class="py-2 pr-4 font-medium">
                      {item.ruleName ?? "-"}
                    </td>
                    <td class="py-2 pr-4">
                      <Badge
                        variant={getConditionBadgeVariant(item.conditionType)}
                      >
                        {getConditionLabel(item.conditionType)}
                      </Badge>
                    </td>
                    <td class="py-2 pr-4 text-muted-foreground">
                      {formatDate(item.startedAt)}
                    </td>
                    <td class="py-2 pr-4 text-muted-foreground">
                      {formatDuration(item.startedAt, item.endedAt)}
                    </td>
                    <td class="py-2 text-muted-foreground">
                      {#if item.acknowledgedAt}
                        {formatDate(item.acknowledgedAt)}
                        {#if item.acknowledgedBy}
                          <span class="text-xs ml-1">
                            by {item.acknowledgedBy}
                          </span>
                        {/if}
                      {:else}
                        <span class="text-muted-foreground/50">-</span>
                      {/if}
                    </td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </div>

          <!-- Pagination -->
          {#if (history.totalPages ?? 1) > 1}
            <div class="flex items-center justify-between mt-4 pt-4 border-t">
              <p class="text-sm text-muted-foreground">
                Page {history.page ?? 1} of {history.totalPages ?? 1}
                ({history.totalCount ?? 0} total)
              </p>
              <div class="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={historyPage <= 1 || historyLoading}
                  onclick={() => loadHistory(historyPage - 1)}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={historyPage >= (history.totalPages ?? 1) ||
                    historyLoading}
                  onclick={() => loadHistory(historyPage + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          {/if}
        {/if}
      </CardContent>
    </Card>
  {/if}
</div>

<RuleEditorSheet bind:open={editorOpen} rule={editingRule} onSave={handleEditorSave} />
