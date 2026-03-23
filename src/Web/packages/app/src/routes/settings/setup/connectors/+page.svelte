<script lang="ts">
  import { goto } from "$app/navigation";
  import { Button } from "$lib/components/ui/button";
  import ConnectorSetup from "$lib/components/connectors/ConnectorSetup.svelte";
</script>

<svelte:head>
  <title>Connect Data Sources - Setup - Nocturne</title>
</svelte:head>

<div class="container mx-auto p-6 max-w-3xl">
  <div class="mb-6">
    <h1 class="text-2xl font-bold tracking-tight">Connect Data Sources</h1>
    <p class="text-muted-foreground">
      Set up a server-side connector to start pulling data into Nocturne
    </p>
  </div>

  <ConnectorSetup
    onComplete={() => goto("/settings/setup/profile")}
    onCancel={() => goto("/settings")}
  >
    {#snippet resultActions({ reset })}
      <Button variant="outline" onclick={reset}>Add Another</Button>
    {/snippet}
  </ConnectorSetup>
</div>
