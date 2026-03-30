<script lang="ts">
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import * as Dialog from "$lib/components/ui/dialog";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Switch } from "$lib/components/ui/switch";
  import {
    Building2,
    Pencil,
    Loader2,
    AlertTriangle,
  } from "lucide-svelte";
  import * as Alert from "$lib/components/ui/alert";
  import * as tenantRemote from "$api/generated/tenants.generated.remote";
  import { getMultitenancyInfo } from "$api/generated/metadatas.generated.remote";
  import type { TenantDetailDto } from "$api";

  // State
  let loading = $state(true);
  let loadError = $state<string | null>(null);
  let tenant = $state<TenantDetailDto | null>(null);

  // Edit dialog state
  let isEditDialogOpen = $state(false);
  let editDisplayName = $state("");
  let editIsActive = $state(true);
  let editSaving = $state(false);

  async function loadTenant() {
    loading = true;
    loadError = null;
    try {
      const mtInfo = await getMultitenancyInfo();
      if (!mtInfo?.currentTenantId) {
        loadError = "Could not determine the current tenant.";
        return;
      }
      tenant = await tenantRemote.getById(mtInfo.currentTenantId);
    } catch {
      loadError = "Failed to load tenant details.";
    } finally {
      loading = false;
    }
  }

  $effect(() => {
    loadTenant();
  });

  function openEditDialog() {
    if (!tenant) return;
    editDisplayName = tenant.displayName ?? "";
    editIsActive = tenant.isActive ?? true;
    isEditDialogOpen = true;
  }

  async function saveEdit() {
    if (!tenant?.id) return;
    editSaving = true;
    try {
      await tenantRemote.update({
        id: tenant.id,
        request: { displayName: editDisplayName, isActive: editIsActive },
      });
      isEditDialogOpen = false;
      await loadTenant();
    } catch {
      // error is handled by remote
    } finally {
      editSaving = false;
    }
  }
</script>

<div class="container mx-auto max-w-4xl p-6 space-y-6">
  <div class="flex items-center gap-3">
    <Building2 class="h-8 w-8 text-primary" />
    <div>
      <h1 class="text-2xl font-bold">Tenant Management</h1>
      <p class="text-muted-foreground">
        Manage the current tenant's details and members
      </p>
    </div>
  </div>

  {#if loading}
    <div class="flex items-center justify-center py-12">
      <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
    </div>
  {:else if loadError}
    <Alert.Root variant="destructive">
      <AlertTriangle class="h-4 w-4" />
      <Alert.Title>Error</Alert.Title>
      <Alert.Description>{loadError}</Alert.Description>
    </Alert.Root>
  {:else if tenant}
    <Card>
      <CardHeader class="flex flex-row items-center justify-between">
        <div>
          <CardTitle>{tenant.displayName}</CardTitle>
          <CardDescription class="font-mono">{tenant.slug}</CardDescription>
        </div>
        <Button variant="outline" size="sm" onclick={openEditDialog}>
          <Pencil class="mr-2 h-4 w-4" />
          Edit
        </Button>
      </CardHeader>
      <CardContent class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <div>
            <p class="text-sm font-medium text-muted-foreground">Status</p>
            <div class="mt-1">
              {#if tenant.isActive}
                <Badge variant="default">Active</Badge>
              {:else}
                <Badge variant="destructive">Inactive</Badge>
              {/if}
            </div>
          </div>
          <div>
            <p class="text-sm font-medium text-muted-foreground">Type</p>
            <div class="mt-1">
              {#if tenant.isDefault}
                <Badge variant="secondary">Default</Badge>
              {:else}
                <Badge variant="outline">Standard</Badge>
              {/if}
            </div>
          </div>
          <div>
            <p class="text-sm font-medium text-muted-foreground">Slug</p>
            <p class="mt-1 font-mono text-sm">{tenant.slug}</p>
          </div>
          <div>
            <p class="text-sm font-medium text-muted-foreground">Created</p>
            <p class="mt-1 text-sm">
              {tenant.sysCreatedAt
                ? new Date(tenant.sysCreatedAt).toLocaleDateString()
                : "---"}
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  {/if}
</div>

<!-- Edit Tenant Dialog -->
<Dialog.Root bind:open={isEditDialogOpen}>
  <Dialog.Content class="max-w-md">
    <Dialog.Header>
      <Dialog.Title>Edit Tenant</Dialog.Title>
      <Dialog.Description>
        Update the tenant's display name and active status
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4 py-4">
      <div class="space-y-2">
        <Label for="edit-display-name">Display Name</Label>
        <Input
          id="edit-display-name"
          bind:value={editDisplayName}
          placeholder="Display Name"
        />
      </div>
      <div class="flex items-center justify-between">
        <Label for="edit-active">Active</Label>
        <Switch id="edit-active" bind:checked={editIsActive} />
      </div>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (isEditDialogOpen = false)}>
        Cancel
      </Button>
      <Button
        onclick={saveEdit}
        disabled={editSaving || !editDisplayName.trim()}
      >
        {#if editSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
        {/if}
        Save
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

