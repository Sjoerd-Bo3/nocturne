<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import * as Card from "$lib/components/ui/card";
  import * as Dialog from "$lib/components/ui/dialog";
  import { Badge } from "$lib/components/ui/badge";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import {
    Fingerprint,
    Plus,
    Trash2,
    Clock,
    ShieldAlert,
    RefreshCw,
    Copy,
    Check,
    AlertTriangle,
    Loader2,
    Info,
    Server,
  } from "lucide-svelte";
  import { startRegistration } from "@simplewebauthn/browser";
  import { formatDate } from "$lib/utils/formatting";
  import {
    registerOptions,
    registerComplete,
    listCredentials,
    removeCredential,
    getRecoveryStatus,
    regenerateRecoveryCodes,
  } from "$lib/api/generated/passkeys.generated.remote";
  import { page } from "$app/state";

  // ============================================================================
  // State
  // ============================================================================

  const credentialsQuery = listCredentials();
  const recoveryQuery = getRecoveryStatus();

  let errorMessage = $state<string | null>(null);
  let successMessage = $state<string | null>(null);

  // Passkey add flow
  let isRegistering = $state(false);
  let showLabelDialog = $state(false);
  let newPasskeyLabel = $state("");

  // Passkey remove flow
  let isRemoving = $state<string | null>(null);
  let showRemoveDialog = $state(false);
  let removeTarget = $state<{ id?: string; label?: string | null } | null>(null);

  // Recovery codes
  let showRegenerateDialog = $state(false);
  let isRegenerating = $state(false);
  let showNewCodesDialog = $state(false);
  let newRecoveryCodes = $state<string[]>([]);
  let copiedCodes = $state(false);

  const user = $derived(page.data?.user);
  const credentials = $derived(credentialsQuery.current?.credentials ?? []);
  const hasOidcLink = $derived(credentialsQuery.current?.hasOidcLink ?? false);
  const recoveryStatus = $derived(recoveryQuery.current);
  const isLoading = $derived(credentialsQuery.loading);
  const canRemovePasskey = $derived(credentials.length > 1 || hasOidcLink);
  const maxPasskeys = 20;

  // ============================================================================
  // Passkey registration
  // ============================================================================

  async function handleAddPasskey() {
    if (!user?.subjectId || !user?.name) return;
    isRegistering = true;
    errorMessage = null;

    try {
      const response = await registerOptions({ subjectId: user.subjectId, username: user.name });
      const options = JSON.parse(response.options ?? "");
      const challengeToken = response.challengeToken ?? "";

      const attestation = await startRegistration({ optionsJSON: options });

      await registerComplete({
        attestationResponseJson: JSON.stringify(attestation),
        challengeToken,
      });

      newPasskeyLabel = "";
      showLabelDialog = true;
    } catch (err) {
      errorMessage = err instanceof Error ? err.message : "Failed to register passkey.";
    } finally {
      isRegistering = false;
    }
  }

  function handleLabelDialogClose() {
    showLabelDialog = false;
    newPasskeyLabel = "";
    successMessage = "Passkey added successfully.";
    clearMessages();
  }

  // ============================================================================
  // Passkey removal
  // ============================================================================

  function confirmRemovePasskey(credential: { id?: string; label?: string | null }) {
    removeTarget = credential;
    showRemoveDialog = true;
  }

  async function handleRemovePasskey() {
    if (!removeTarget?.id) return;
    isRemoving = removeTarget.id;
    errorMessage = null;
    showRemoveDialog = false;

    try {
      await removeCredential(removeTarget.id);
      successMessage = "Passkey removed.";
      clearMessages();
    } catch (err) {
      errorMessage =
        err instanceof Error ? err.message : "Failed to remove passkey.";
    } finally {
      isRemoving = null;
      removeTarget = null;
    }
  }

  // ============================================================================
  // Recovery codes
  // ============================================================================

  async function handleRegenerateCodes() {
    isRegenerating = true;
    errorMessage = null;
    showRegenerateDialog = false;

    try {
      const result = await regenerateRecoveryCodes();
      newRecoveryCodes = result.codes ?? [];
      showNewCodesDialog = true;
    } catch (err) {
      errorMessage = "Failed to regenerate recovery codes.";
    } finally {
      isRegenerating = false;
    }
  }

  async function copyRecoveryCodes() {
    const text = newRecoveryCodes.join("\n");
    await navigator.clipboard.writeText(text);
    copiedCodes = true;
    setTimeout(() => (copiedCodes = false), 2000);
  }

  function clearMessages() {
    setTimeout(() => {
      successMessage = null;
      errorMessage = null;
    }, 3000);
  }
</script>

<svelte:head>
  <title>Security - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto max-w-4xl p-6 space-y-6">
  <div class="space-y-1">
    <h1 class="text-2xl font-bold tracking-tight">Security</h1>
    <p class="text-muted-foreground">
      Manage your passkeys and account recovery options
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

  {#if isLoading}
    <Card.Root>
      <Card.Content class="flex items-center justify-center py-12">
        <Loader2 class="h-6 w-6 animate-spin text-muted-foreground" />
      </Card.Content>
    </Card.Root>
  {:else}
    <!-- Section 1: Registered Passkeys -->
    <Card.Root>
      <Card.Header>
        <div class="flex items-center justify-between">
          <div>
            <Card.Title class="flex items-center gap-2">
              <Fingerprint class="h-5 w-5" />
              Passkeys
            </Card.Title>
            <Card.Description>
              Passkeys provide secure, phishing-resistant authentication using
              your device's biometrics or security key.
            </Card.Description>
          </div>
          <Button
            variant="outline"
            size="sm"
            disabled={isRegistering || credentials.length >= maxPasskeys}
            onclick={handleAddPasskey}
          >
            {#if isRegistering}
              <Loader2 class="mr-1.5 h-3.5 w-3.5 animate-spin" />
            {:else}
              <Plus class="mr-1.5 h-3.5 w-3.5" />
            {/if}
            Add passkey
          </Button>
        </div>
      </Card.Header>
      <Card.Content class="space-y-3">
        {#if credentials.length === 0}
          <div class="flex flex-col items-center justify-center py-8 text-center">
            <div
              class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-muted"
            >
              <Fingerprint class="h-6 w-6 text-muted-foreground" />
            </div>
            <p class="text-sm text-muted-foreground max-w-sm">
              No passkeys registered. Add a passkey to enable passwordless
              sign-in.
            </p>
          </div>
        {:else}
          {#each credentials as credential (credential.id)}
            <div
              class="flex items-center justify-between gap-4 rounded-md border p-3"
            >
              <div class="space-y-1 flex-1 min-w-0">
                <p class="text-sm font-medium">
                  {credential.label ?? "Unnamed passkey"}
                </p>
                <div
                  class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground"
                >
                  <span class="flex items-center gap-1">
                    <Clock class="h-3 w-3" />
                    Created {formatDate(credential.createdAt)}
                  </span>
                  {#if credential.lastUsedAt}
                    <span class="flex items-center gap-1">
                      <Clock class="h-3 w-3" />
                      Last used {formatDate(credential.lastUsedAt)}
                    </span>
                  {/if}
                </div>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                class="text-destructive hover:text-destructive shrink-0"
                disabled={!canRemovePasskey ||
                  isRemoving === credential.id}
                onclick={() => confirmRemovePasskey(credential)}
              >
                {#if isRemoving === credential.id}
                  <Loader2 class="h-3.5 w-3.5 animate-spin" />
                {:else}
                  <Trash2 class="h-3.5 w-3.5" />
                {/if}
              </Button>
            </div>
          {/each}
        {/if}

        {#if credentials.length >= maxPasskeys}
          <p class="text-xs text-muted-foreground">
            Maximum of {maxPasskeys} passkeys reached.
          </p>
        {/if}

        {#if !canRemovePasskey && credentials.length > 0}
          <p class="text-xs text-muted-foreground">
            You cannot remove your only passkey without an alternative sign-in
            method linked to your account.
          </p>
        {/if}
      </Card.Content>
    </Card.Root>

    <!-- Section 2: Recovery Codes -->
    <Card.Root>
      <Card.Header>
        <Card.Title class="flex items-center gap-2">
          <ShieldAlert class="h-5 w-5" />
          Recovery Codes
        </Card.Title>
        <Card.Description>
          Recovery codes allow you to access your account if you lose all your
          passkeys. Store them in a safe place.
        </Card.Description>
      </Card.Header>
      <Card.Content class="space-y-4">
        {#if recoveryStatus}
          <div class="flex items-center justify-between">
            <div class="space-y-1">
              <p class="text-sm font-medium">
                {recoveryStatus.remainingCodes} of {recoveryStatus.totalCodes} recovery
                codes remaining
              </p>
              <p class="text-xs text-muted-foreground">
                Each code can only be used once.
              </p>
            </div>
            <Badge
              variant={(recoveryStatus.remainingCodes ?? 0) > 2
                ? "secondary"
                : "destructive"}
            >
              {recoveryStatus.remainingCodes} remaining
            </Badge>
          </div>
        {:else}
          <p class="text-sm text-muted-foreground">
            No recovery codes have been generated yet.
          </p>
        {/if}

        <Separator />

        <Button
          variant="outline"
          disabled={isRegenerating}
          onclick={() => (showRegenerateDialog = true)}
        >
          {#if isRegenerating}
            <Loader2 class="mr-1.5 h-4 w-4 animate-spin" />
          {:else}
            <RefreshCw class="mr-1.5 h-4 w-4" />
          {/if}
          Regenerate recovery codes
        </Button>
      </Card.Content>
    </Card.Root>

    <!-- Section 3: Recovery Mode Info -->
    <Card.Root class="border-muted">
      <Card.Header>
        <Card.Title class="flex items-center gap-2 text-muted-foreground">
          <Server class="h-5 w-5" />
          Server-Side Account Recovery
        </Card.Title>
      </Card.Header>
      <Card.Content>
        <div
          class="flex items-start gap-3 rounded-md border border-border bg-muted/30 p-4"
        >
          <Info class="mt-0.5 h-5 w-5 shrink-0 text-muted-foreground" />
          <div class="space-y-2">
            <p class="text-sm text-muted-foreground">
              If you lose all your passkeys and recovery codes, you can recover
              your account by setting the
              <code
                class="rounded bg-muted px-1.5 py-0.5 text-xs font-mono text-foreground"
              >
                NOCTURNE_RECOVERY_MODE
              </code>
              environment variable on your server.
            </p>
            <p class="text-sm text-muted-foreground">
              This enables a temporary recovery flow that allows you to register
              a new passkey. It requires physical access to the server
              environment.
            </p>
          </div>
        </div>
      </Card.Content>
    </Card.Root>
  {/if}
</div>

<!-- Label Dialog (after passkey registration) -->
<Dialog.Root bind:open={showLabelDialog}>
  <Dialog.Content>
    <Dialog.Header>
      <Dialog.Title>Name your passkey</Dialog.Title>
      <Dialog.Description>
        Give this passkey a name to help you identify it later (e.g. "MacBook
        Touch ID", "YubiKey 5").
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4 py-4">
      <div class="space-y-2">
        <Label for="passkey-label">Label (optional)</Label>
        <Input
          id="passkey-label"
          type="text"
          placeholder="e.g. MacBook Touch ID"
          bind:value={newPasskeyLabel}
        />
      </div>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onclick={handleLabelDialogClose}>
        Skip
      </Button>
      <Button onclick={handleLabelDialogClose}>
        Save
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Remove Confirmation Dialog -->
<Dialog.Root bind:open={showRemoveDialog}>
  <Dialog.Content>
    <Dialog.Header>
      <Dialog.Title>Remove passkey</Dialog.Title>
      <Dialog.Description>
        Are you sure you want to remove "{removeTarget?.label ??
          "Unnamed passkey"}"? You will no longer be able to sign in with this
        passkey.
      </Dialog.Description>
    </Dialog.Header>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (showRemoveDialog = false)}>
        Cancel
      </Button>
      <Button variant="destructive" onclick={handleRemovePasskey}>
        Remove
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Regenerate Confirmation Dialog -->
<Dialog.Root bind:open={showRegenerateDialog}>
  <Dialog.Content>
    <Dialog.Header>
      <Dialog.Title>Regenerate recovery codes</Dialog.Title>
      <Dialog.Description>
        This will invalidate all existing recovery codes and generate new ones.
        Make sure to save the new codes in a safe place.
      </Dialog.Description>
    </Dialog.Header>
    <Dialog.Footer>
      <Button
        variant="outline"
        onclick={() => (showRegenerateDialog = false)}
      >
        Cancel
      </Button>
      <Button variant="destructive" onclick={handleRegenerateCodes}>
        Regenerate
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- New Recovery Codes Dialog -->
<Dialog.Root bind:open={showNewCodesDialog}>
  <Dialog.Content class="max-w-md">
    <Dialog.Header>
      <Dialog.Title>New recovery codes</Dialog.Title>
      <Dialog.Description>
        Save these codes in a safe place. Each code can only be used once. This
        is the only time they will be shown.
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4 py-4">
      <div class="grid grid-cols-2 gap-2 rounded-md border bg-muted/30 p-4">
        {#each newRecoveryCodes as code}
          <p class="font-mono text-sm text-center">{code}</p>
        {/each}
      </div>
      <Button variant="outline" class="w-full" onclick={copyRecoveryCodes}>
        {#if copiedCodes}
          <Check class="mr-1.5 h-4 w-4 text-green-600" />
          Copied
        {:else}
          <Copy class="mr-1.5 h-4 w-4" />
          Copy all codes
        {/if}
      </Button>
    </div>
    <Dialog.Footer>
      <Button
        onclick={() => {
          showNewCodesDialog = false;
          newRecoveryCodes = [];
          copiedCodes = false;
        }}
      >
        Done
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
