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
  import * as Tabs from "$lib/components/ui/tabs";
  import * as Dialog from "$lib/components/ui/dialog";
  import * as AlertDialog from "$lib/components/ui/alert-dialog";
  import * as Select from "$lib/components/ui/select";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Switch } from "$lib/components/ui/switch";
  import {
    Building2,
    Users,
    Pencil,
    Plus,
    Trash2,
    Loader2,
    AlertTriangle,
    Shield,
  } from "lucide-svelte";
  import * as Alert from "$lib/components/ui/alert";
  import * as tenantRemote from "$api/generated/tenants.generated.remote";
  import { getMultitenancyInfo } from "$api/generated/metadatas.generated.remote";
  import type { TenantDetailDto, TenantMemberDto } from "$api";

  const roleLabels: Record<string, string> = {
    owner: "Owner",
    caretaker: "Caretaker",
    readonly: "Read Only",
  };

  const roleVariants: Record<string, "default" | "secondary" | "outline"> = {
    owner: "default",
    caretaker: "secondary",
    readonly: "outline",
  };

  // State
  let activeTab = $state("details");
  let loading = $state(true);
  let loadError = $state<string | null>(null);
  let tenant = $state<TenantDetailDto | null>(null);

  // Edit dialog state
  let isEditDialogOpen = $state(false);
  let editDisplayName = $state("");
  let editIsActive = $state(true);
  let editSaving = $state(false);

  // Add member dialog state
  let isAddMemberDialogOpen = $state(false);
  let newMemberSubjectId = $state("");
  let newMemberRole = $state("readonly");
  let addMemberSaving = $state(false);

  // Remove member state
  let removingMember = $state<TenantMemberDto | null>(null);
  let isRemoveDialogOpen = $state(false);
  let removeSaving = $state(false);

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

  function openAddMemberDialog() {
    newMemberSubjectId = "";
    newMemberRole = "readonly";
    isAddMemberDialogOpen = true;
  }

  async function addMember() {
    if (!tenant?.id) return;
    addMemberSaving = true;
    try {
      await tenantRemote.addMember({
        id: tenant.id,
        request: { subjectId: newMemberSubjectId, role: newMemberRole },
      });
      isAddMemberDialogOpen = false;
      await loadTenant();
    } catch {
      // error is handled by remote
    } finally {
      addMemberSaving = false;
    }
  }

  function confirmRemoveMember(member: TenantMemberDto) {
    removingMember = member;
    isRemoveDialogOpen = true;
  }

  async function removeMember() {
    if (!tenant?.id || !removingMember?.subjectId) return;
    removeSaving = true;
    try {
      await tenantRemote.removeMember({
        id: tenant.id,
        subjectId: removingMember.subjectId,
      });
      isRemoveDialogOpen = false;
      removingMember = null;
      await loadTenant();
    } catch {
      // error is handled by remote
    } finally {
      removeSaving = false;
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
    <Tabs.Root bind:value={activeTab}>
      <Tabs.List>
        <Tabs.Trigger value="details">
          <Building2 class="mr-2 h-4 w-4" />
          Details
        </Tabs.Trigger>
        <Tabs.Trigger value="members">
          <Users class="mr-2 h-4 w-4" />
          Members
          {#if tenant.members?.length}
            <Badge variant="secondary" class="ml-2">
              {tenant.members.length}
            </Badge>
          {/if}
        </Tabs.Trigger>
      </Tabs.List>

      <!-- Details Tab -->
      <Tabs.Content value="details">
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
                    : "—"}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      </Tabs.Content>

      <!-- Members Tab -->
      <Tabs.Content value="members">
        <Card>
          <CardHeader class="flex flex-row items-center justify-between">
            <div>
              <CardTitle>Members</CardTitle>
              <CardDescription>
                Users who have access to this tenant
              </CardDescription>
            </div>
            <Button variant="default" size="sm" onclick={openAddMemberDialog}>
              <Plus class="mr-2 h-4 w-4" />
              Add Member
            </Button>
          </CardHeader>
          <CardContent>
            {#if !tenant.members?.length}
              <div
                class="flex flex-col items-center justify-center py-8 text-center"
              >
                <Users class="h-12 w-12 text-muted-foreground/50 mb-4" />
                <p class="text-muted-foreground">No members found</p>
              </div>
            {:else}
              <div class="space-y-3">
                {#each tenant.members as member (member.subjectId)}
                  <div
                    class="flex items-center justify-between rounded-lg border p-3"
                  >
                    <div class="flex items-center gap-3">
                      <Shield class="h-5 w-5 text-muted-foreground" />
                      <div>
                        <p class="font-mono text-sm">{member.subjectId}</p>
                        <p class="text-xs text-muted-foreground">
                          Joined {member.sysCreatedAt
                            ? new Date(member.sysCreatedAt).toLocaleDateString()
                            : ""}
                        </p>
                      </div>
                    </div>
                    <div class="flex items-center gap-2">
                      <Badge
                        variant={roleVariants[member.role ?? "readonly"] ??
                          "outline"}
                      >
                        {roleLabels[member.role ?? "readonly"] ?? member.role}
                      </Badge>
                      <Button
                        variant="ghost"
                        size="icon"
                        onclick={() => confirmRemoveMember(member)}
                      >
                        <Trash2 class="h-4 w-4 text-destructive" />
                      </Button>
                    </div>
                  </div>
                {/each}
              </div>
            {/if}
          </CardContent>
        </Card>
      </Tabs.Content>
    </Tabs.Root>
  {/if}
</div>

<!-- Edit Tenant Dialog -->
<Dialog.Root bind:open={isEditDialogOpen}>
  <Dialog.Content class="max-w-md">
    <Dialog.Header>
      <Dialog.Title>Edit Tenan</Dialog.Title>
      <Dialog.Description>
        Update the tenant's display name and active status
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4 py-4">
      <div class="space-y-2">
        <Label for="edit-display-name">Display Nam</Label>
        <Input
          id="edit-display-name"
          bind:value={editDisplayName}
          placeholder="Display Name"
        />
      </div>
      <div class="flex items-center justify-between">
        <Label for="edit-active">Activ</Label>
        <Switch id="edit-active" bind:checked={editIsActive} />
      </div>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (isEditDialogOpen = false)}>
        Cance
      </Button>
      <Button
        onclick={saveEdit}
        disabled={editSaving || !editDisplayName.trim()}
      >
        {#if editSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
        {/if}
        Sav
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Add Member Dialog -->
<Dialog.Root bind:open={isAddMemberDialogOpen}>
  <Dialog.Content class="max-w-md">
    <Dialog.Header>
      <Dialog.Title>Add Membe</Dialog.Title>
      <Dialog.Description>Add a user to this tenant</Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4 py-4">
      <div class="space-y-2">
        <Label for="member-subject-id">Subject I</Label>
        <Input
          id="member-subject-id"
          bind:value={newMemberSubjectId}
          placeholder="00000000-0000-0000-0000-000000000000"
          class="font-mono"
        />
      </div>
      <div class="space-y-2">
        <Label>Rol</Label>
        <Select.Root type="single" bind:value={newMemberRole}>
          <Select.Trigger>
            {roleLabels[newMemberRole] ?? newMemberRole}
          </Select.Trigger>
          <Select.Content>
            <Select.Item value="owner" label="Owner" />
            <Select.Item value="caretaker" label="Caretaker" />
            <Select.Item value="readonly" label="Read Only" />
          </Select.Content>
        </Select.Root>
      </div>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (isAddMemberDialogOpen = false)}>
        Cance
      </Button>
      <Button
        onclick={addMember}
        disabled={addMemberSaving || !newMemberSubjectId.trim()}
      >
        {#if addMemberSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
        {/if}
        Ad
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Remove Member Confirmation -->
<AlertDialog.Root bind:open={isRemoveDialogOpen}>
  <AlertDialog.Content>
    <AlertDialog.Header>
      <AlertDialog.Title>Remove membe</AlertDialog.Title>
      <AlertDialog.Description>
        Remove this member from the tenant? They will lose access to all tenant
        data
      </AlertDialog.Description>
    </AlertDialog.Header>
    <AlertDialog.Footer>
      <AlertDialog.Cancel>Cance</AlertDialog.Cancel>
      <AlertDialog.Action onclick={removeMember} disabled={removeSaving}>
        {#if removeSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
        {/if}
        Remov
      </AlertDialog.Action>
    </AlertDialog.Footer>
  </AlertDialog.Content>
</AlertDialog.Root>
