<script lang="ts">
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import { Switch } from "$lib/components/ui/switch";
  import { Label } from "$lib/components/ui/label";
  import { update } from "$api/generated/tenants.generated.remote";
  import { getMultitenancyInfo } from "$api/generated/metadatas.generated.remote";

  const multitenancyQuery = $derived(getMultitenancyInfo());
  const tenantId = $derived(multitenancyQuery.current?.currentTenantId ?? null);

  let allowAccessRequests = $state(true);

  async function save(): Promise<boolean> {
    if (!tenantId) return true;
    try {
      await update({
        id: tenantId,
        request: { allowAccessRequests },
      });
      return true;
    } catch (err) {
      console.error("Failed to update sharing settings:", err);
      return false;
    }
  }
</script>

<svelte:head>
  <title>Sharing - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Sharing"
  description="Configure how others can request access to your instance"
  currentStep={5}
  totalSteps={8}
  prevHref="/settings/setup/insulins"
  nextHref="/settings/setup/uploaders"
  showSkip={true}
  onSave={save}
>
  <div class="space-y-6">
    <div class="flex items-center justify-between rounded-lg border p-4">
      <div class="space-y-1">
        <Label class="text-sm font-medium">Allow access requests</Label>
        <p class="text-xs text-muted-foreground">
          People who visit your site can request access. You'll be notified to approve or deny each request.
        </p>
      </div>
      <Switch bind:checked={allowAccessRequests} />
    </div>
  </div>
</WizardShell>
