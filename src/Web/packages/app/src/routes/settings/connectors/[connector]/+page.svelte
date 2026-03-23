<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button";
  import { ChevronLeft } from "lucide-svelte";
  import ConnectorSetup from "$lib/components/connectors/ConnectorSetup.svelte";

  const connectorName = $derived(page.params.connector);
</script>

<svelte:head>
  <title>{connectorName} - Connectors - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto p-6 max-w-3xl space-y-6">
  <div>
    <Button
      variant="ghost"
      size="sm"
      href="/settings/connectors"
      class="gap-1 -ml-2 mb-4"
    >
      <ChevronLeft class="h-4 w-4" />
      Back to connectors
    </Button>
  </div>

  <ConnectorSetup
    connectorId={connectorName}
    showToggle
    showDangerZone
    showCapabilities
    primaryAction="save-only"
    onCancel={() => goto("/settings/connectors")}
  />
</div>
