<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import * as Card from "$lib/components/ui/card";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import {
    Users,
    Trash2,
    Check,
    AlertTriangle,
    Clock,
    Link,
    Copy,
    Loader2,
  } from "lucide-svelte";
  import { formatDate } from "$lib/utils/formatting";

  import { getFollowers } from "$lib/api/generated/memberinvites.generated.remote";
  import {
    listInvites,
    createInvite,
    revokeInvite,
    removeMember,
  } from "$api/generated/tenants.generated.remote";

  /** Human-readable descriptions for each scope. */
  const scopeDescriptions: Record<string, string> = {
    "entries.read": "View glucose readings",
    "treatments.read": "View treatments",
    "devicestatus.read": "View device status",
    "profile.read": "View profile settings",
    "notifications.read": "View notifications",
    "reports.read": "View reports and analytics",
  };

  /** Available scopes for follower invites (read-only subset). */
  const followerScopes = [
    "entries.read",
    "treatments.read",
    "devicestatus.read",
    "profile.read",
    "notifications.read",
    "reports.read",
  ] as const;

  // Remote queries
  const followersQuery = $derived(getFollowers());
  const invitesQuery = $derived(listInvites());

  // Derived data from queries
  const followers = $derived(followersQuery.current?.members ?? []);
  const invites = $derived(invitesQuery.current?.invites ?? []);
  const activeInvites = $derived(invites.filter((i) => i.isValid));

  // UI state
  let showCreateInvite = $state(false);
  let inviteLabel = $state("");
  let inviteScopes = $state<Record<string, boolean>>({
    "entries.read": true,
    "treatments.read": false,
    "devicestatus.read": false,
    "profile.read": false,
    "notifications.read": false,
    "reports.read": false,
  });
  let allowMultipleUses = $state(false);
  let limitTo24Hours = $state(false);
  let createdInviteUrl = $state<string | null>(null);
  let copiedInvite = $state(false);

  // Loading/error states
  let isRevoking = $state<string | null>(null);
  let isCreatingInvite = $state(false);
  let isRevokingInvite = $state<string | null>(null);
  let errorMessage = $state<string | null>(null);
  let successMessage = $state<string | null>(null);

  const inviteScopeList = $derived(
    Object.entries(inviteScopes)
      .filter(([, v]) => v)
      .map(([k]) => k)
  );

  /** Reset the create-invite form to its defaults. */
  function resetInviteForm() {
    inviteLabel = "";
    inviteScopes = {
      "entries.read": true,
      "treatments.read": false,
      "devicestatus.read": false,
      "profile.read": false,
      "notifications.read": false,
      "reports.read": false,
    };
    allowMultipleUses = false;
    limitTo24Hours = false;
    showCreateInvite = false;
    createdInviteUrl = null;
    errorMessage = null;
  }

  /** Copy invite URL to clipboard */
  async function copyInviteUrl() {
    if (createdInviteUrl) {
      await navigator.clipboard.writeText(createdInviteUrl);
      copiedInvite = true;
      setTimeout(() => (copiedInvite = false), 2000);
    }
  }

  /** Clear messages after a delay */
  function clearMessages() {
    setTimeout(() => {
      successMessage = null;
      errorMessage = null;
    }, 3000);
  }

  /** Handle revoking a follower membership */
  async function handleRevokeMember(subjectId: string) {
    isRevoking = subjectId;
    errorMessage = null;
    try {
      await removeMember(subjectId);
      successMessage = "Follower removed successfully.";
      clearMessages();
    } catch (err) {
      errorMessage = "Failed to remove follower. Please try again.";
      clearMessages();
    } finally {
      isRevoking = null;
    }
  }

  /** Handle creating an invite */
  async function handleCreateInvite() {
    isCreatingInvite = true;
    errorMessage = null;
    try {
      const result = await createInvite({
        role: "follower",
        scopes: inviteScopeList,
        label: inviteLabel || undefined,
        expiresInDays: 7,
        maxUses: allowMultipleUses ? undefined : 1,
        limitTo24Hours,
      });
      if (result.inviteUrl) {
        createdInviteUrl = result.inviteUrl;
      }
    } catch (err) {
      errorMessage = "Failed to create invite. Please try again.";
    } finally {
      isCreatingInvite = false;
    }
  }

  /** Handle revoking an invite */
  async function handleRevokeInvite(inviteId: string) {
    isRevokingInvite = inviteId;
    errorMessage = null;
    try {
      await revokeInvite(inviteId);
      successMessage = "Invite revoked successfully.";
      clearMessages();
    } catch (err) {
      errorMessage = "Failed to revoke invite. Please try again.";
      clearMessages();
    } finally {
      isRevokingInvite = null;
    }
  }
</script>

<svelte:head>
  <title>Followers & Sharing - Settings - Nocturne</title>
</svelte:head>

<div class="w-full py-6 space-y-6">
  <div class="space-y-1">
    <h1 class="text-2xl font-bold tracking-tight">Followers & Sharing</h1>
    <p class="text-muted-foreground">
      Share your data with caregivers and family members
    </p>
  </div>

  {#if errorMessage}
    <div
      class="flex items-start gap-3 rounded-md border border-destructive/20 bg-destructive/5 p-3"
    >
      <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
      <p class="text-sm text-destructive">{errorMessage}</p>
    </div>
  {/if}

  {#if successMessage}
    <div
      class="flex items-start gap-3 rounded-md border border-green-200 bg-green-50 p-3 dark:border-green-900/50 dark:bg-green-900/20"
    >
      <Check
        class="mt-0.5 h-4 w-4 shrink-0 text-green-600 dark:text-green-400"
      />
      <p class="text-sm text-green-800 dark:text-green-200">
        {successMessage}
      </p>
    </div>
  {/if}

  <div class="space-y-4">
    <div class="flex items-center justify-between gap-4">
      <p class="text-sm text-muted-foreground">
        Share your data with caregivers and family members
      </p>
      <div class="flex gap-2">
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
      </div>
    </div>

    <!-- Create Invite Link Card -->
    {#if showCreateInvite}
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-lg">Create Invite Link</Card.Title>
          <Card.Description>
            Generate a shareable link. Anyone with this link can accept the
            invite after signing in.
          </Card.Description>
        </Card.Header>
        <Card.Content>
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
                  Invite link created! Share it with your friend or family
                  member.
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
                  placeholder="e.g. Mom, Endocrinologist"
                  bind:value={inviteLabel}
                />
              </div>

              <div class="space-y-3">
                <Label>Data to share</Label>
                <div class="grid gap-3 sm:grid-cols-2">
                  {#each followerScopes as scope}
                    <div class="flex items-center gap-2">
                      <Checkbox
                        id="invite-scope-{scope}"
                        checked={inviteScopes[scope]}
                        onCheckedChange={(checked) => {
                          inviteScopes[scope] = checked === true;
                        }}
                      />
                      <label
                        for="invite-scope-{scope}"
                        class="text-sm text-foreground cursor-pointer select-none"
                      >
                        {scopeDescriptions[scope] ?? scope}
                      </label>
                    </div>
                  {/each}
                </div>
              </div>

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
                    By default, invite links can only be used once. Enable this
                    to allow unlimited uses.
                  </p>
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
                    Restrict access to only the most recent 24 hours of data.
                    Older data will not be visible to the follower.
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
                  disabled={inviteScopeList.length === 0 || isCreatingInvite}
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
        </Card.Content>
      </Card.Root>
    {/if}

    <!-- Pending Invites -->
    {#if activeInvites.length > 0 && !showCreateInvite}
      <Card.Root>
        <Card.Header class="pb-3">
          <Card.Title class="text-base flex items-center gap-2">
            <Link class="h-4 w-4" />
            Pending Invites
          </Card.Title>
        </Card.Header>
        <Card.Content class="space-y-3">
          {#each activeInvites as invite (invite.id)}
            <div
              class="flex items-center justify-between gap-4 rounded-md border p-3"
            >
              <div class="space-y-1 flex-1 min-w-0">
                <p class="text-sm font-medium">
                  {invite.label ?? "Invite Link"}
                </p>
                <p class="text-xs text-muted-foreground">
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
                {#if invite.usedBy && invite.usedBy.length > 0}
                  <div class="mt-2 pt-2 border-t space-y-1">
                    <p
                      class="text-xs font-medium text-muted-foreground uppercase tracking-wider"
                    >
                      Used by
                    </p>
                    {#each invite.usedBy as usage}
                      <p class="text-xs text-foreground">
                        <Check class="inline h-3 w-3 mr-1 text-primary" />
                        {usage.name ?? "Unknown"}
                        <span class="text-muted-foreground ml-1">
                          on {formatDate(usage.usedAt)}
                        </span>
                      </p>
                    {/each}
                  </div>
                {/if}
              </div>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                class="text-destructive hover:text-destructive shrink-0"
                disabled={isRevokingInvite === invite.id}
                onclick={() => handleRevokeInvite(invite.id!)}
              >
                {#if isRevokingInvite === invite.id}
                  <Loader2 class="h-3.5 w-3.5 animate-spin" />
                {:else}
                  <Trash2 class="h-3.5 w-3.5" />
                {/if}
              </Button>
            </div>
          {/each}
        </Card.Content>
      </Card.Root>
    {/if}

    {#if followers.length === 0}
      <Card.Root>
        <Card.Content
          class="flex flex-col items-center justify-center py-12 text-center"
        >
          <div
            class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-muted"
          >
            <Users class="h-6 w-6 text-muted-foreground" />
          </div>
          <p class="text-sm text-muted-foreground max-w-sm">
            No followers. Share your data with caregivers by creating an invite
            link.
          </p>
        </Card.Content>
      </Card.Root>
    {:else}
      {#each followers as follower (follower.subjectId)}
        <Card.Root>
          <Card.Header>
            <div class="flex items-start justify-between gap-4">
              <div class="space-y-1 flex-1 min-w-0">
                <Card.Title class="flex items-center gap-2 flex-wrap">
                  <span class="truncate">
                    {follower.name ?? "Unknown"}
                  </span>
                </Card.Title>
                <Card.Description>
                  {#if follower.label}
                    {follower.label}
                  {/if}
                </Card.Description>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                class="text-destructive border-destructive/30 hover:bg-destructive/10 shrink-0"
                disabled={isRevoking === follower.subjectId}
                onclick={() => handleRevokeMember(follower.subjectId!)}
              >
                {#if isRevoking === follower.subjectId}
                  <Loader2 class="mr-1.5 h-3.5 w-3.5 animate-spin" />
                {:else}
                  <Trash2 class="mr-1.5 h-3.5 w-3.5" />
                {/if}
                Revoke
              </Button>
            </div>
          </Card.Header>
          <Card.Content class="space-y-4">
            <div>
              <p
                class="mb-2 text-xs font-medium text-muted-foreground uppercase tracking-wider"
              >
                Shared Data
              </p>
              <ul class="space-y-1.5">
                {#each follower.scopes ?? [] as scope}
                  <li class="flex items-start gap-2 text-sm">
                    <Check class="mt-0.5 h-3.5 w-3.5 shrink-0 text-primary" />
                    <span class="text-muted-foreground">
                      {scopeDescriptions[scope] ?? scope}
                    </span>
                  </li>
                {/each}
              </ul>
            </div>

            {#if follower.limitTo24Hours}
              <div
                class="flex items-center gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 dark:border-amber-900/50 dark:bg-amber-900/20"
              >
                <Clock
                  class="h-3.5 w-3.5 shrink-0 text-amber-600 dark:text-amber-400"
                />
                <p class="text-xs text-amber-800 dark:text-amber-200">
                  Limited to last 24 hours of data
                </p>
              </div>
            {/if}

            <Separator />

            <div
              class="flex flex-wrap gap-x-6 gap-y-1 text-xs text-muted-foreground"
            >
              <span class="flex items-center gap-1.5">
                <Clock class="h-3 w-3" />
                Created {formatDate(follower.sysCreatedAt)}
              </span>
              {#if follower.lastUsedAt}
                <span class="flex items-center gap-1.5">
                  <Clock class="h-3 w-3" />
                  Last used {formatDate(follower.lastUsedAt)}
                </span>
              {/if}
            </div>
          </Card.Content>
        </Card.Root>
      {/each}
    {/if}
  </div>
</div>
