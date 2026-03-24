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
  import { Checkbox } from "$lib/components/ui/checkbox";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Switch } from "$lib/components/ui/switch";
  import {
    Building2,
    Users,
    Pencil,
    Trash2,
    Loader2,
    AlertTriangle,
    Shield,
    Link,
    Copy,
    Check,
    Clock,
  } from "lucide-svelte";
  import * as Alert from "$lib/components/ui/alert";
  import * as tenantRemote from "$api/generated/tenants.generated.remote";
  import { getMultitenancyInfo } from "$api/generated/metadatas.generated.remote";
  import {
    createInvite as createTenantInvite,
    listInvites as listTenantInvites,
    revokeInvite as revokeTenantInvite,
  } from "$api/generated/tenants.generated.remote";
  import type { TenantDetailDto, TenantMemberDto } from "$api";
  import { formatDate } from "$lib/utils/formatting";

  const roleOptions = ["owner", "admin", "caretaker", "follower"] as const;
  type Role = (typeof roleOptions)[number];

  const roleLabels: Record<string, string> = {
    owner: "Owner",
    admin: "Admin",
    caretaker: "Caretaker",
    follower: "Follower",
  };

  const roleVariants: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
    owner: "default",
    admin: "default",
    caretaker: "secondary",
    follower: "outline",
  };

  /** Human-readable descriptions for each scope. */
  const scopeDescriptions: Record<string, string> = {
    "entries.read": "View glucose readings",
    "entries.readwrite": "View and record glucose readings",
    "treatments.read": "View treatments",
    "treatments.readwrite": "View and record treatments",
    "devicestatus.read": "View device status",
    "devicestatus.readwrite": "View and update device status",
    "profile.read": "View profile settings",
    "profile.readwrite": "View and update profile settings",
    "notifications.read": "View notifications",
    "notifications.readwrite": "Manage notifications",
    "reports.read": "View reports and analytics",
    "identity.read": "View basic account info",
    "sharing.readwrite": "Manage sharing settings",
    "health.read": "View all health data (read-only)",
    "*": "Full access (including delete)",
  };

  /** Available scopes for follower invites. */
  const availableScopes = [
    "entries.read",
    "entries.readwrite",
    "treatments.read",
    "treatments.readwrite",
    "devicestatus.read",
    "devicestatus.readwrite",
    "profile.read",
    "profile.readwrite",
    "notifications.read",
    "notifications.readwrite",
    "reports.read",
    "identity.read",
    "sharing.readwrite",
    "health.read",
    "*",
  ] as const;

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

  // Invite link flow state
  let showCreateInvite = $state(false);
  let inviteLabel = $state("");
  let inviteRole = $state<Role>("caretaker");
  let inviteScopes = $state<Record<string, boolean>>({});
  let allowMultipleUses = $state(false);
  let limitTo24Hours = $state(false);
  let createdInviteUrl = $state<string | null>(null);
  let copiedInvite = $state(false);
  let isCreatingInvite = $state(false);

  // Invite listing state
  let invites = $state<any[]>([]);
  let invitesLoading = $state(false);
  let isRevokingInvite = $state<string | null>(null);

  // Remove member state
  let removingMember = $state<TenantMemberDto | null>(null);
  let isRemoveDialogOpen = $state(false);
  let removeSaving = $state(false);

  const inviteScopeList = $derived(
    Object.entries(inviteScopes)
      .filter(([, v]) => v)
      .map(([k]) => k),
  );

  const activeInvites = $derived(invites.filter((i: any) => i.isValid));

  /** Whether the create button should be disabled. */
  const isCreateDisabled = $derived(
    isCreatingInvite ||
      (inviteRole === "follower" && inviteScopeList.length === 0),
  );

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

  async function loadInvites() {
    invitesLoading = true;
    try {
      const result = await listTenantInvites();
      invites = result?.invites ?? [];
    } catch {
      // error is handled by remote
    } finally {
      invitesLoading = false;
    }
  }

  $effect(() => {
    loadTenant();
    loadInvites();
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

  /** Reset the invite form to its defaults. */
  function resetInviteForm() {
    inviteLabel = "";
    inviteRole = "caretaker";
    inviteScopes = {};
    allowMultipleUses = false;
    limitTo24Hours = false;
    showCreateInvite = false;
    createdInviteUrl = null;
  }

  /** Copy the invite URL to clipboard. */
  async function copyInviteUrl() {
    if (createdInviteUrl) {
      await navigator.clipboard.writeText(createdInviteUrl);
      copiedInvite = true;
      setTimeout(() => (copiedInvite = false), 2000);
    }
  }

  /** Handle creating an invite link. */
  async function handleCreateInvite() {
    isCreatingInvite = true;
    try {
      const result = await createTenantInvite({
        role: inviteRole,
        scopes: inviteRole === "follower" ? inviteScopeList : undefined,
        label: inviteLabel || undefined,
        expiresInDays: 7,
        maxUses: allowMultipleUses ? undefined : 1,
        limitTo24Hours: inviteRole === "follower" ? limitTo24Hours : undefined,
      });
      if (result.inviteUrl) {
        createdInviteUrl = result.inviteUrl;
        await loadInvites();
      }
    } catch {
      // error is handled by remote
    } finally {
      isCreatingInvite = false;
    }
  }

  /** Handle revoking an invite. */
  async function handleRevokeInvite(inviteId: string) {
    isRevokingInvite = inviteId;
    try {
      await revokeTenantInvite(inviteId);
      await loadInvites();
    } catch {
      // error is handled by remote
    } finally {
      isRevokingInvite = null;
    }
  }

  /** Handle changing a member's role. */
  async function handleRoleChange(member: TenantMemberDto, newRole: string) {
    if (!tenant?.id || !member.subjectId) return;
    // TODO: Call remote function to update member role after NSwag regeneration
    // await tenantRemote.updateMemberRole({ id: tenant.id, subjectId: member.subjectId, role: newRole });
    // await loadTenant();
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
                    : "---"}
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
            {#if !showCreateInvite}
              <Button
                variant="outline"
                size="sm"
                onclick={() => (showCreateInvite = true)}
              >
                <Link class="mr-1.5 h-3.5 w-3.5" />
                Create Invite Link
              </Button>
            {/if}
          </CardHeader>
          <CardContent class="space-y-4">
            <!-- Create Invite Link Card -->
            {#if showCreateInvite}
              <div class="rounded-lg border bg-card p-4 space-y-4">
                <div>
                  <h3 class="text-lg font-semibold">Create Invite Link</h3>
                  <p class="text-sm text-muted-foreground">
                    Generate a shareable link. Anyone with this link can join this
                    tenant after signing in.
                  </p>
                </div>

                {#if createdInviteUrl}
                  <!-- Show the created invite URL -->
                  <div class="space-y-4">
                    <div
                      class="flex items-start gap-3 rounded-md border border-green-200 bg-green-50 p-3 dark:border-green-900/50 dark:bg-green-900/20"
                    >
                      <Check
                        class="mt-0.5 h-4 w-4 shrink-0 text-green-600 dark:text-green-400"
                      />
                      <p class="text-sm text-green-800 dark:text-green-200">
                        Invite link created. Share it with the new member.
                      </p>
                    </div>

                    <div class="flex gap-2">
                      <Input
                        type="text"
                        value={createdInviteUrl}
                        readonly
                        class="font-mono text-sm"
                      />
                      <Button variant="outline" size="icon" onclick={copyInviteUrl}>
                        {#if copiedInvite}
                          <Check class="h-4 w-4 text-green-600" />
                        {:else}
                          <Copy class="h-4 w-4" />
                        {/if}
                      </Button>
                    </div>

                    <Button
                      variant="outline"
                      class="w-full"
                      onclick={() => resetInviteForm()}
                    >
                      Done
                    </Button>
                  </div>
                {:else}
                  <!-- Show the create invite form -->
                  <div class="space-y-4">
                    <div class="space-y-2">
                      <Label for="invite-label">Label (optional)</Label>
                      <Input
                        id="invite-label"
                        type="text"
                        placeholder="e.g. New caretaker, Endocrinologist"
                        bind:value={inviteLabel}
                      />
                    </div>

                    <div class="space-y-2">
                      <Label>Role</Label>
                      <Select.Root
                        type="single"
                        value={inviteRole}
                        onValueChange={(v) => {
                          if (v) inviteRole = v as Role;
                        }}
                      >
                        <Select.Trigger class="w-full">
                          {roleLabels[inviteRole] ?? inviteRole}
                        </Select.Trigger>
                        <Select.Content>
                          {#each roleOptions as role}
                            <Select.Item value={role}>
                              {roleLabels[role]}
                            </Select.Item>
                          {/each}
                        </Select.Content>
                      </Select.Root>
                    </div>

                    {#if inviteRole === "follower"}
                      <div class="space-y-3">
                        <Label>Scopes</Label>
                        <div class="grid gap-2 max-h-64 overflow-y-auto pr-1">
                          {#each availableScopes as scope}
                            <div class="flex items-center gap-2">
                              <Checkbox
                                id="invite-scope-{scope}"
                                checked={inviteScopes[scope] ?? false}
                                onCheckedChange={(checked) => {
                                  inviteScopes[scope] = checked === true;
                                }}
                              />
                              <label
                                for="invite-scope-{scope}"
                                class="text-sm cursor-pointer select-none flex-1"
                              >
                                <span class="font-mono text-xs text-muted-foreground">
                                  {scope}
                                </span>
                                <span class="text-muted-foreground ml-1">
                                  --- {scopeDescriptions[scope] ?? scope}
                                </span>
                              </label>
                            </div>
                          {/each}
                        </div>
                      </div>

                      <div
                        class="flex items-start gap-2 rounded-md border p-3 bg-muted/30"
                      >
                        <Checkbox
                          id="limit-to-24-hours"
                          checked={limitTo24Hours}
                          onCheckedChange={(checked) => {
                            limitTo24Hours = checked === true;
                          }}
                        />
                        <div class="flex-1">
                          <label
                            for="limit-to-24-hours"
                            class="text-sm font-medium cursor-pointer select-none"
                          >
                            Only last 24 hours
                          </label>
                          <p class="text-xs text-muted-foreground mt-0.5">
                            Restrict access to only the most recent 24 hours of
                            data. Older data will not be visible to the member.
                          </p>
                        </div>
                      </div>
                    {/if}

                    <div
                      class="flex items-start gap-2 rounded-md border p-3 bg-muted/30"
                    >
                      <Checkbox
                        id="allow-multiple-uses"
                        checked={allowMultipleUses}
                        onCheckedChange={(checked) => {
                          allowMultipleUses = checked === true;
                        }}
                      />
                      <div class="flex-1">
                        <label
                          for="allow-multiple-uses"
                          class="text-sm font-medium cursor-pointer select-none"
                        >
                          Allow multiple uses
                        </label>
                        <p class="text-xs text-muted-foreground mt-0.5">
                          By default, invite links can only be used once. Enable
                          this to allow unlimited uses.
                        </p>
                      </div>
                    </div>

                    <div class="flex gap-3">
                      <Button
                        type="button"
                        variant="outline"
                        class="flex-1"
                        onclick={() => resetInviteForm()}
                      >
                        Cancel
                      </Button>
                      <Button
                        type="button"
                        class="flex-1"
                        disabled={isCreateDisabled}
                        onclick={handleCreateInvite}
                      >
                        {#if isCreatingInvite}
                          <Loader2 class="mr-1.5 h-4 w-4 animate-spin" />
                        {/if}
                        Create Link
                      </Button>
                    </div>
                  </div>
                {/if}
              </div>
            {/if}

            <!-- Active Invites -->
            {#if activeInvites.length > 0 && !showCreateInvite}
              <div class="space-y-3">
                <div class="flex items-center gap-2 text-sm font-medium text-muted-foreground">
                  <Link class="h-4 w-4" />
                  Pending Invites
                </div>
                {#each activeInvites as invite (invite.id)}
                  <div
                    class="flex items-center justify-between gap-4 rounded-md border p-3"
                  >
                    <div class="space-y-1 flex-1 min-w-0">
                      <div class="flex items-center gap-2">
                        <p class="text-sm font-medium">
                          {invite.label ?? "Invite Link"}
                        </p>
                        {#if invite.role}
                          <Badge
                            variant={roleVariants[invite.role] ?? "outline"}
                            class="text-xs"
                          >
                            {roleLabels[invite.role] ?? invite.role}
                          </Badge>
                        {/if}
                      </div>
                      <p class="text-xs text-muted-foreground">
                        <Clock class="inline h-3 w-3 mr-0.5" />
                        Expires {formatDate(invite.expiresAt)}
                        {#if invite.maxUses}
                          &middot; {invite.useCount}/{invite.maxUses} uses
                        {:else}
                          &middot; {invite.useCount}
                          {invite.useCount === 1 ? "use" : "uses"}
                        {/if}
                        {#if invite.limitTo24Hours}
                          &middot; Last 24 hours only
                        {/if}
                      </p>
                    </div>
                    <Button
                      variant="ghost"
                      size="icon"
                      disabled={isRevokingInvite === invite.id}
                      onclick={() => handleRevokeInvite(invite.id)}
                    >
                      {#if isRevokingInvite === invite.id}
                        <Loader2 class="h-4 w-4 animate-spin" />
                      {:else}
                        <Trash2 class="h-4 w-4 text-destructive" />
                      {/if}
                    </Button>
                  </div>
                {/each}
              </div>
            {/if}

            <!-- Members list -->
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
                        <p class="text-sm font-medium">
                          {member.name ?? member.subjectId}
                        </p>
                        <p class="text-xs text-muted-foreground font-mono">
                          {member.subjectId}
                        </p>
                        <p class="text-xs text-muted-foreground">
                          Joined {member.sysCreatedAt
                            ? new Date(member.sysCreatedAt).toLocaleDateString()
                            : ""}
                        </p>
                      </div>
                    </div>
                    <div class="flex items-center gap-2">
                      <Select.Root
                        type="single"
                        value={member.role ?? "follower"}
                        onValueChange={(v) => {
                          if (v && v !== member.role) handleRoleChange(member, v);
                        }}
                      >
                        <Select.Trigger class="w-[130px] h-8 text-xs">
                          {roleLabels[member.role ?? "follower"] ?? member.role}
                        </Select.Trigger>
                        <Select.Content>
                          {#each roleOptions as role}
                            <Select.Item value={role}>
                              {roleLabels[role]}
                            </Select.Item>
                          {/each}
                        </Select.Content>
                      </Select.Root>
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

<!-- Remove Member Confirmation -->
<AlertDialog.Root bind:open={isRemoveDialogOpen}>
  <AlertDialog.Content>
    <AlertDialog.Header>
      <AlertDialog.Title>Remove member</AlertDialog.Title>
      <AlertDialog.Description>
        Remove this member from the tenant? They will lose access to all tenant
        data.
      </AlertDialog.Description>
    </AlertDialog.Header>
    <AlertDialog.Footer>
      <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
      <AlertDialog.Action onclick={removeMember} disabled={removeSaving}>
        {#if removeSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
        {/if}
        Remove
      </AlertDialog.Action>
    </AlertDialog.Footer>
  </AlertDialog.Content>
</AlertDialog.Root>
