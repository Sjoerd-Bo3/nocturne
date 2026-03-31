<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import * as Card from "$lib/components/ui/card";
  import { ChevronLeft, CheckCircle2, ArrowRight } from "lucide-svelte";
  import ConnectorSetup from "$lib/components/connectors/ConnectorSetup.svelte";
  import type { SyncResult } from "$lib/api/generated/nocturne-api-client";

  let migrationComplete = $state(false);
  let syncResult = $state<SyncResult | null>(null);

  function handleMigrationComplete(result: SyncResult) {
    syncResult = result;
    migrationComplete = true;
  }
</script>

<svelte:head>
  <title>Nightscout Migration - Setup - Nocturne</title>
</svelte:head>

<div class="container mx-auto p-6 max-w-3xl space-y-6">
  <div>
    <Button
      variant="ghost"
      size="sm"
      href="/settings/setup/migrate"
      class="gap-1 -ml-2 mb-4"
    >
      <ChevronLeft class="h-4 w-4" />
      Back
    </Button>
  </div>

  {#if migrationComplete}
    <Card.Root class="border-green-500/30">
      <Card.Header class="flex flex-row items-center gap-4 space-y-0 p-6">
        <div
          class="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-green-100 text-green-600 dark:bg-green-900/30 dark:text-green-400"
        >
          <CheckCircle2 class="h-6 w-6" />
        </div>
        <div class="flex-1 min-w-0">
          <Card.Title class="text-lg font-semibold">
            Migration started successfully
          </Card.Title>
          <Card.Description class="mt-1">
            Your Nightscout data is being synced. You can continue setting up
            Nocturne while the sync runs in the background.
          </Card.Description>
        </div>
      </Card.Header>
      <Card.Content class="pt-0 px-6 pb-6">
        <Button href="/settings/setup" class="gap-2">
          Continue to setup
          <ArrowRight class="h-4 w-4" />
        </Button>
      </Card.Content>
    </Card.Root>
  {:else}
    <div>
      <h1 class="text-2xl font-bold tracking-tight">
        Connect to Nightscout
      </h1>
      <p class="text-muted-foreground mt-1">
        Enter your Nightscout URL and API secret to begin migrating your data
      </p>
    </div>

    <ConnectorSetup
      connectorId="nightscout"
      primaryAction="save-and-sync"
      showToggle={false}
      showDangerZone={false}
      showCapabilities={true}
      onComplete={handleMigrationComplete}
    />
  {/if}
</div>
