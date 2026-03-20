<script lang="ts">
  import { onMount } from "svelte";
  import {
    getConnectorConfiguration,
    getConnectorSchema,
    getConnectorEffectiveConfig,
    getAllConnectorStatus,
    saveConnectorConfiguration,
    saveConnectorSecrets,
    setConnectorActive,
    type JsonSchema,
  } from "$api/connectorConfig.remote";
  import {
    getServicesOverview,
    triggerConnectorSync,
  } from "$api/generated/services.generated.remote";
  import ConnectorConfigForm from "$lib/components/settings/ConnectorConfigForm.svelte";
  import type {
    AvailableConnector,
    ConnectorConfigurationResponse,
    ConnectorStatusInfo,
    SyncResult,
  } from "$lib/api/generated/nocturne-api-client";
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import {
    AlertCircle,
    CheckCircle,
    ChevronLeft,
    Loader2,
    Plus,
    Plug,
  } from "lucide-svelte";

  // ── View state ──────────────────────────────────────────────────────

  type ViewState = "selection" | "configuring" | "syncing" | "results";
  let viewState = $state<ViewState>("selection");
  let selectedConnector = $state<AvailableConnector | null>(null);

  // ── Connector list state ────────────────────────────────────────────

  let availableConnectors = $state<AvailableConnector[]>([]);
  let connectorStatuses = $state<ConnectorStatusInfo[]>([]);
  let configuredIds = $state<Set<string>>(new Set());
  let isLoading = $state(true);
  let loadError = $state<string | null>(null);

  // ── Configuration state ─────────────────────────────────────────────

  let schema = $state<JsonSchema | null>(null);
  let existingConfig = $state<ConnectorConfigurationResponse | null>(null);
  let effectiveConfig = $state<Record<string, unknown> | null>(null);
  let configuration = $state<Record<string, unknown>>({});
  let secrets = $state<Record<string, string>>({});
  let connectorStatus = $state<ConnectorStatusInfo | null>(null);
  let isSaving = $state(false);
  let configError = $state<string | null>(null);

  // ── Sync state ──────────────────────────────────────────────────────

  let syncResult = $state<SyncResult | null>(null);

  // ── Derived ─────────────────────────────────────────────────────────

  const hasSecrets = $derived(connectorStatus?.hasSecrets ?? false);
  const hasRuntimeConfig = $derived(
    schema && schema.properties && Object.keys(schema.properties).length > 0
  );

  // ── Load connector list ─────────────────────────────────────────────

  onMount(async () => {
    await loadConnectorList();
  });

  async function loadConnectorList() {
    isLoading = true;
    loadError = null;

    try {
      const [overview, statuses] = await Promise.all([
        getServicesOverview(),
        getAllConnectorStatus().catch(() => [] as ConnectorStatusInfo[]),
      ]);

      availableConnectors =
        overview?.availableConnectors?.filter((c) => c.available) ?? [];
      connectorStatuses = statuses ?? [];

      // Build set of configured connector IDs
      const ids = new Set<string>();
      for (const s of connectorStatuses) {
        if (s.isEnabled && s.connectorName) {
          ids.add(s.connectorName.toLowerCase());
        }
      }
      configuredIds = ids;
    } catch (e) {
      loadError =
        e instanceof Error ? e.message : "Failed to load connectors";
    } finally {
      isLoading = false;
    }
  }

  function isConfigured(connector: AvailableConnector): boolean {
    return configuredIds.has(connector.id?.toLowerCase() ?? "");
  }

  // ── Select and configure a connector ────────────────────────────────

  async function selectConnector(connector: AvailableConnector) {
    selectedConnector = connector;
    viewState = "configuring";
    configError = null;
    schema = null;

    try {
      const connectorId = connector.id!;

      const [schemaResult, configResult, effectiveResult] = await Promise.all([
        getConnectorSchema(connectorId),
        getConnectorConfiguration(connectorId).catch(() => null),
        getConnectorEffectiveConfig(connectorId).catch(() => null),
      ]);

      schema = schemaResult;
      existingConfig = configResult;
      effectiveConfig = effectiveResult;

      // Get connector status
      try {
        const statuses = await getAllConnectorStatus();
        connectorStatus =
          statuses?.find(
            (s) =>
              s.connectorName?.toLowerCase() === connectorId.toLowerCase()
          ) ?? null;
      } catch {
        connectorStatus = null;
      }

      // Initialize configuration
      const configData =
        existingConfig?.configuration?.rootElement ??
        existingConfig?.configuration;
      if (
        configData &&
        typeof configData === "object" &&
        Object.keys(configData).length > 0
      ) {
        configuration = { ...configData };
      } else {
        configuration = getDefaultsFromSchema(schemaResult);
      }

      secrets = {};
    } catch (e) {
      configError =
        e instanceof Error
          ? e.message
          : "Failed to load connector configuration";
    }
  }

  function getDefaultsFromSchema(schema: JsonSchema): Record<string, unknown> {
    const defaults: Record<string, unknown> = {};
    for (const [propName, propSchema] of Object.entries(schema.properties)) {
      if (propSchema.default !== undefined) {
        defaults[propName] = propSchema.default;
      }
    }
    return defaults;
  }

  // ── Save handlers ───────────────────────────────────────────────────

  async function handleSaveConfiguration(config: Record<string, unknown>) {
    if (!selectedConnector?.id) return;

    isSaving = true;
    const result = await saveConnectorConfiguration({
      connectorName: selectedConnector.id,
      configuration: config,
    });
    isSaving = false;

    if (!result.success) {
      configError = result.error || "Failed to save configuration";
    }
  }

  async function handleSaveSecrets(newSecrets: Record<string, string>) {
    if (!selectedConnector?.id) return;

    isSaving = true;
    const result = await saveConnectorSecrets({
      connectorName: selectedConnector.id,
      secrets: newSecrets,
    });
    isSaving = false;

    if (!result.success) {
      configError = result.error || "Failed to save credentials";
    }
  }

  // ── Save, enable, and sync ──────────────────────────────────────────

  async function handleSaveAndSync() {
    if (!selectedConnector?.id) return;

    isSaving = true;
    configError = null;

    try {
      // Save configuration
      const saveResult = await saveConnectorConfiguration({
        connectorName: selectedConnector.id,
        configuration,
      });

      if (!saveResult.success) {
        configError = saveResult.error || "Failed to save configuration";
        isSaving = false;
        return;
      }

      // Save secrets if any were entered
      const nonEmptySecrets = Object.fromEntries(
        Object.entries(secrets).filter(([, v]) => v && v.trim())
      );
      if (Object.keys(nonEmptySecrets).length > 0) {
        const secretResult = await saveConnectorSecrets({
          connectorName: selectedConnector.id,
          secrets: nonEmptySecrets,
        });
        if (!secretResult.success) {
          configError =
            secretResult.error || "Failed to save credentials";
          isSaving = false;
          return;
        }
      }

      // Enable the connector
      await setConnectorActive({
        connectorName: selectedConnector.id,
        isActive: true,
      });

      isSaving = false;

      // Trigger sync
      viewState = "syncing";
      const result = await triggerConnectorSync({
        id: selectedConnector.id,
        request: {},
      });
      syncResult = result as SyncResult;
      viewState = "results";
    } catch (e) {
      isSaving = false;
      configError =
        e instanceof Error ? e.message : "An error occurred during setup";
      viewState = "results";
      syncResult = {
        success: false,
        message:
          e instanceof Error ? e.message : "An error occurred during sync",
        errors: [
          e instanceof Error ? e.message : "An error occurred during sync",
        ],
      };
    }
  }

  // ── Navigation helpers ──────────────────────────────────────────────

  function handleAddAnother() {
    // Mark current connector as configured
    if (selectedConnector?.id) {
      configuredIds = new Set([
        ...configuredIds,
        selectedConnector.id.toLowerCase(),
      ]);
    }

    // Reset state and go back to selection
    selectedConnector = null;
    schema = null;
    existingConfig = null;
    effectiveConfig = null;
    configuration = {};
    secrets = {};
    connectorStatus = null;
    syncResult = null;
    configError = null;
    viewState = "selection";
  }

  function handleBackToSelection() {
    selectedConnector = null;
    schema = null;
    configError = null;
    syncResult = null;
    viewState = "selection";
  }
</script>

<svelte:head>
  <title>Connectors - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Connect Data Sources"
  description="Connect to your CGM or diabetes management platform to sync glucose and treatment data. You can always add more connectors later."
  currentStep={4}
  totalSteps={5}
  prevHref="/settings/setup/insulins"
  nextHref="/settings/setup/profile"
  showSkip={true}
  saveDisabled={false}
  onSave={async () => true}
>
  {#if viewState === "selection"}
    <!-- ── Connector Selection ──────────────────────────────────── -->
    {#if isLoading}
      <div class="flex items-center justify-center py-12">
        <Loader2 class="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    {:else if loadError}
      <Card class="border-destructive">
        <CardContent class="flex items-center gap-3 pt-6">
          <AlertCircle class="h-5 w-5 text-destructive" />
          <div>
            <p class="font-medium">Failed to load connectors</p>
            <p class="text-sm text-muted-foreground">{loadError}</p>
          </div>
        </CardContent>
      </Card>
    {:else if availableConnectors.length === 0}
      <Card>
        <CardContent class="py-8 text-center">
          <Plug class="h-12 w-12 mx-auto mb-4 text-muted-foreground" />
          <p class="font-medium">No connectors available</p>
          <p class="text-sm text-muted-foreground mt-1">
            There are no data source connectors available at this time.
          </p>
        </CardContent>
      </Card>
    {:else}
      <div class="grid gap-3">
        {#each availableConnectors as connector (connector.id)}
          {@const configured = isConfigured(connector)}
          <button
            type="button"
            class="w-full text-left"
            onclick={() => selectConnector(connector)}
          >
            <Card
              class="transition-colors hover:bg-muted/50 cursor-pointer {configured
                ? 'border-green-500/50'
                : ''}"
            >
              <CardContent class="flex items-center gap-4 py-4">
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2">
                    <p class="font-medium">{connector.name}</p>
                    {#if connector.category}
                      <Badge variant="outline" class="text-xs">
                        {connector.category}
                      </Badge>
                    {/if}
                    {#if configured}
                      <CheckCircle
                        class="h-4 w-4 text-green-500 flex-shrink-0"
                      />
                    {/if}
                  </div>
                  {#if connector.description}
                    <p class="text-sm text-muted-foreground mt-0.5 truncate">
                      {connector.description}
                    </p>
                  {/if}
                </div>
              </CardContent>
            </Card>
          </button>
        {/each}
      </div>
    {/if}

  {:else if viewState === "configuring"}
    <!-- ── Connector Configuration ──────────────────────────────── -->
    <div class="space-y-4">
      <Button
        variant="ghost"
        size="sm"
        class="gap-1 -ml-2"
        onclick={handleBackToSelection}
      >
        <ChevronLeft class="h-4 w-4" />
        Back to connectors
      </Button>

      <div>
        <h2 class="text-lg font-semibold">{selectedConnector?.name}</h2>
        {#if selectedConnector?.description}
          <p class="text-sm text-muted-foreground">
            {selectedConnector.description}
          </p>
        {/if}
      </div>

      {#if configError}
        <Card class="border-destructive">
          <CardContent class="flex items-center gap-3 py-3">
            <AlertCircle class="h-5 w-5 text-destructive" />
            <p class="text-sm">{configError}</p>
          </CardContent>
        </Card>
      {/if}

      {#if schema && hasRuntimeConfig}
        <ConnectorConfigForm
          {schema}
          bind:configuration
          bind:secrets
          {effectiveConfig}
          {hasSecrets}
          onSaveConfiguration={handleSaveConfiguration}
          onSaveSecrets={handleSaveSecrets}
          {isSaving}
        />
      {:else if schema}
        <Card>
          <CardContent class="py-8 text-center">
            <p class="font-medium">No configuration required</p>
            <p class="text-sm text-muted-foreground mt-1">
              This connector can be enabled without additional configuration.
            </p>
          </CardContent>
        </Card>
      {:else if !configError}
        <div class="flex items-center justify-center py-8">
          <Loader2 class="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      {/if}

      {#if schema}
        <div class="flex justify-end pt-2">
          <Button onclick={handleSaveAndSync} disabled={isSaving}>
            {#if isSaving}
              <Loader2 class="mr-2 h-4 w-4 animate-spin" />
              Saving...
            {:else}
              Save & Sync
            {/if}
          </Button>
        </div>
      {/if}
    </div>

  {:else if viewState === "syncing"}
    <!-- ── Syncing ──────────────────────────────────────────────── -->
    <Card>
      <CardContent class="py-12 text-center">
        <Loader2
          class="h-8 w-8 animate-spin mx-auto mb-4 text-muted-foreground"
        />
        <p class="font-medium">Syncing {selectedConnector?.name}...</p>
        <p class="text-sm text-muted-foreground mt-1">
          This may take a moment depending on how much data is available.
        </p>
      </CardContent>
    </Card>

  {:else if viewState === "results"}
    <!-- ── Sync Results ─────────────────────────────────────────── -->
    <div class="space-y-4">
      {#if syncResult?.success}
        <Card class="border-green-500/50">
          <CardHeader>
            <CardTitle class="flex items-center gap-2">
              <CheckCircle class="h-5 w-5 text-green-500" />
              Sync Successful
            </CardTitle>
            {#if syncResult.message}
              <CardDescription>{syncResult.message}</CardDescription>
            {/if}
          </CardHeader>
          {#if syncResult.itemsSynced && Object.keys(syncResult.itemsSynced).length > 0}
            <CardContent>
              <div class="space-y-1">
                {#each Object.entries(syncResult.itemsSynced as Record<string, number>) as [type, count]}
                  <div
                    class="flex items-center justify-between text-sm"
                  >
                    <span class="text-muted-foreground">{type}</span>
                    <span class="font-medium">{count} items</span>
                  </div>
                {/each}
              </div>
            </CardContent>
          {/if}
        </Card>
      {:else}
        <Card class="border-destructive/50">
          <CardHeader>
            <CardTitle class="flex items-center gap-2">
              <AlertCircle class="h-5 w-5 text-destructive" />
              Sync {syncResult ? "Failed" : "Error"}
            </CardTitle>
            {#if syncResult?.message}
              <CardDescription>{syncResult.message}</CardDescription>
            {/if}
          </CardHeader>
          {#if syncResult?.errors && syncResult.errors.length > 0}
            <CardContent>
              <ul class="text-sm text-muted-foreground space-y-1">
                {#each syncResult.errors as err}
                  <li>{err}</li>
                {/each}
              </ul>
            </CardContent>
          {/if}
        </Card>

        <p class="text-sm text-muted-foreground">
          You can continue setup and troubleshoot the connector later in Settings.
        </p>
      {/if}

      <div class="flex justify-end gap-2 pt-2">
        <Button variant="outline" onclick={handleAddAnother}>
          <Plus class="mr-2 h-4 w-4" />
          Add Another Connector
        </Button>
      </div>
    </div>
  {/if}
</WizardShell>
