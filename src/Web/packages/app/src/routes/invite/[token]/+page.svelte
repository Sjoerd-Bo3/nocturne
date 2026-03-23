<script lang="ts">
  import { enhance } from "$app/forms";
  import { goto, invalidateAll } from "$app/navigation";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Card from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import {
    UserPlus,
    Check,
    AlertTriangle,
    Clock,
    Eye,
    Activity,
    Smartphone,
    User,
    Bell,
    FileText,
    Fingerprint,
    ExternalLink,
    Loader2,
    Copy,
    ShieldCheck,
  } from "lucide-svelte";
  import { getOidcProviders } from "$routes/auth/auth.remote";
  import { acceptInviteWithPasskey } from "$lib/auth/passkey-client";

  const { data, form } = $props();

  /** Human-readable descriptions for each OAuth scope. */
  const scopeDescriptions: Record<string, string> = {
    "entries.read": "View glucose readings",
    "treatments.read": "View treatments",
    "devicestatus.read": "View device status",
    "profile.read": "View profile settings",
    "notifications.read": "View notifications",
    "reports.read": "View reports and analytics",
    "identity.read": "View basic account info",
    "health.read": "View all health data (read-only)",
  };

  /** Icons for scopes */
  const scopeIcons: Record<string, typeof Eye> = {
    "entries.read": Activity,
    "treatments.read": FileText,
    "devicestatus.read": Smartphone,
    "profile.read": User,
    "notifications.read": Bell,
    "reports.read": FileText,
  };

  const invite = $derived(data.invite);
  const isAuthenticated = $derived(data.isAuthenticated);
  const formError = $derived(form?.error as string | undefined);

  // OIDC providers for unauthenticated registration
  const oidcQuery = getOidcProviders();

  // Registration state for unauthenticated users
  let username = $state("");
  let displayName = $state("");
  let isRegistering = $state(false);
  let registrationComplete = $state(false);
  let recoveryCodes = $state<string[]>([]);
  let errorMessage = $state<string | null>(null);
  let codesCopied = $state(false);
  let isRedirecting = $state(false);
  let selectedProvider = $state<string | null>(null);

  const canRegister = $derived(
    username.trim().length > 0 && displayName.trim().length > 0
  );

  async function handlePasskeyRegistration() {
    if (!data.token) return;

    isRegistering = true;
    errorMessage = null;

    try {
      const result = await acceptInviteWithPasskey(
        data.token,
        username.trim(),
        displayName.trim(),
        `${displayName.trim()}'s passkey`
      );

      if (!result.success) {
        errorMessage = result.error || "Failed to register";
        return;
      }

      registrationComplete = true;
      recoveryCodes = result.recoveryCodes ?? [];
    } catch (err) {
      errorMessage =
        err instanceof Error ? err.message : "Registration failed";
    } finally {
      isRegistering = false;
    }
  }

  async function copyRecoveryCodes() {
    const text = recoveryCodes.join("\n");
    try {
      await navigator.clipboard.writeText(text);
      codesCopied = true;
    } catch {
      codesCopied = true;
    }
  }

  async function proceedAfterRegistration() {
    await invalidateAll();
    await goto("/", { invalidateAll: true });
  }

  function loginWithProvider(providerId: string) {
    isRedirecting = true;
    selectedProvider = providerId;

    const params = new URLSearchParams();
    params.set("provider", providerId);
    params.set("returnUrl", `/invite/${data.token}`);

    window.location.href = `/api/auth/login?${params.toString()}`;
  }

  function getButtonStyle(buttonColor?: string): string {
    if (!buttonColor) return "";
    return `background-color: ${buttonColor}; border-color: ${buttonColor};`;
  }
</script>

<svelte:head>
  <title>Accept Invite - Nocturne</title>
</svelte:head>

<div class="flex min-h-screen items-center justify-center p-4">
  <Card.Root class="w-full max-w-md">
    {#if !invite}
      <!-- Invite not found -->
      <Card.Header class="text-center">
        <div class="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-destructive/10">
          <AlertTriangle class="h-8 w-8 text-destructive" />
        </div>
        <Card.Title class="text-xl">Invite Not Found</Card.Title>
        <Card.Description>
          {data.error ?? "This invite link is invalid or has expired."}
        </Card.Description>
      </Card.Header>
      <Card.Content class="text-center">
        <Button href="/auth/login" variant="outline">
          Go to Login
        </Button>
      </Card.Content>
    {:else if !invite.isValid}
      <!-- Invite expired or revoked -->
      <Card.Header class="text-center">
        <div class="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-muted">
          <Clock class="h-8 w-8 text-muted-foreground" />
        </div>
        <Card.Title class="text-xl">
          {#if invite.isExpired}
            Invite Expired
          {:else if invite.isRevoked}
            Invite Revoked
          {:else}
            Invite Unavailable
          {/if}
        </Card.Title>
        <Card.Description>
          {#if invite.isExpired}
            This invite link has expired. Please ask {invite.ownerName ?? "the data owner"} for a new invite.
          {:else if invite.isRevoked}
            This invite link has been revoked by {invite.ownerName ?? "the data owner"}.
          {:else}
            This invite link is no longer available.
          {/if}
        </Card.Description>
      </Card.Header>
      <Card.Content class="text-center">
        <Button href="/auth/login" variant="outline">
          Go to Login
        </Button>
      </Card.Content>
    {:else}
      <!-- Valid invite -->
      <Card.Header class="text-center">
        <div class="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-primary/10">
          <UserPlus class="h-8 w-8 text-primary" />
        </div>
        <Card.Title class="text-xl">You're Invited</Card.Title>
        <Card.Description>
          <span class="font-medium text-foreground">
            {invite.ownerName ?? invite.ownerEmail ?? "Someone"}
          </span>
          wants to share their health data with you
          {#if invite.label}
            <Badge variant="secondary" class="ml-2">{invite.label}</Badge>
          {/if}
        </Card.Description>
      </Card.Header>

      <Card.Content class="space-y-6">
        <!-- What you'll be able to see -->
        <div>
          <p class="mb-3 text-sm font-medium">You'll be able to see:</p>
          <ul class="space-y-2">
            {#each invite.scopes as scope}
              {@const Icon = scopeIcons[scope] ?? Eye}
              <li class="flex items-center gap-3 text-sm">
                <div class="flex h-8 w-8 items-center justify-center rounded-full bg-muted">
                  <Icon class="h-4 w-4 text-muted-foreground" />
                </div>
                <span>{scopeDescriptions[scope] ?? scope}</span>
              </li>
            {/each}
          </ul>
        </div>

        {#if formError}
          <div class="flex items-start gap-3 rounded-md border border-destructive/20 bg-destructive/5 p-3">
            <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
            <p class="text-sm text-destructive">{formError}</p>
          </div>
        {/if}

        {#if errorMessage}
          <div class="flex items-start gap-3 rounded-md border border-destructive/20 bg-destructive/5 p-3">
            <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
            <p class="text-sm text-destructive">{errorMessage}</p>
          </div>
        {/if}

        {#if isAuthenticated}
          <!-- User is logged in - show accept button -->
          <form method="POST" action="?/accept" use:enhance>
            <Button type="submit" class="w-full" size="lg">
              <Check class="mr-2 h-4 w-4" />
              Accept Invite
            </Button>
          </form>
        {:else if registrationComplete}
          <!-- Registration complete - show recovery codes -->
          <div class="space-y-4">
            <div class="flex items-start gap-3 rounded-md border border-green-500/20 bg-green-500/5 p-3">
              <Check class="mt-0.5 h-4 w-4 shrink-0 text-green-600" />
              <p class="text-sm text-green-700 dark:text-green-400">
                Account created and passkey registered.
              </p>
            </div>

            {#if recoveryCodes.length > 0}
              <div class="space-y-3">
                <div class="flex items-center gap-2">
                  <ShieldCheck class="h-5 w-5 text-primary" />
                  <h3 class="font-medium">Recovery Codes</h3>
                </div>
                <p class="text-sm text-muted-foreground">
                  Save these recovery codes in a safe place. Each code can only be used once.
                </p>

                <div class="grid grid-cols-2 gap-2 rounded-lg border bg-muted/50 p-4">
                  {#each recoveryCodes as code}
                    <code class="rounded bg-background px-2 py-1 text-center text-sm font-mono">
                      {code}
                    </code>
                  {/each}
                </div>

                <Button
                  variant={codesCopied ? "outline" : "default"}
                  class="w-full"
                  onclick={copyRecoveryCodes}
                >
                  {#if codesCopied}
                    <Check class="mr-2 h-4 w-4" />
                    Codes copied
                  {:else}
                    <Copy class="mr-2 h-4 w-4" />
                    Copy recovery codes
                  {/if}
                </Button>
              </div>
            {/if}

            <Button
              class="w-full"
              size="lg"
              disabled={recoveryCodes.length > 0 && !codesCopied}
              onclick={proceedAfterRegistration}
            >
              Continue to Nocturne
            </Button>

            {#if recoveryCodes.length > 0 && !codesCopied}
              <p class="text-center text-xs text-muted-foreground">
                Copy your recovery codes before continuing.
              </p>
            {/if}
          </div>
        {:else}
          <!-- User not logged in - show inline registration -->
          <div class="space-y-4">
            <div class="space-y-3">
              <div class="space-y-2">
                <Label for="invite-display-name">Display name</Label>
                <Input
                  id="invite-display-name"
                  type="text"
                  placeholder="Your name"
                  bind:value={displayName}
                  disabled={isRegistering}
                />
              </div>

              <div class="space-y-2">
                <Label for="invite-username">Username</Label>
                <Input
                  id="invite-username"
                  type="text"
                  placeholder="your-username"
                  bind:value={username}
                  disabled={isRegistering}
                />
              </div>

              <Button
                class="w-full"
                size="lg"
                disabled={!canRegister || isRegistering || isRedirecting}
                onclick={handlePasskeyRegistration}
              >
                {#if isRegistering}
                  <Loader2 class="mr-2 h-5 w-5 animate-spin" />
                  Waiting for passkey...
                {:else}
                  <Fingerprint class="mr-2 h-5 w-5" />
                  Register with passkey
                {/if}
              </Button>
            </div>

            {#if oidcQuery.current?.enabled && oidcQuery.current.providers.length > 0}
              <div class="relative">
                <div class="absolute inset-0 flex items-center">
                  <span class="w-full border-t"></span>
                </div>
                <div class="relative flex justify-center text-xs uppercase">
                  <span class="bg-background px-2 text-muted-foreground">
                    Or continue with
                  </span>
                </div>
              </div>

              <div class="space-y-3">
                {#each oidcQuery.current.providers as provider}
                  <Button
                    variant="outline"
                    class="w-full h-11 relative"
                    style={getButtonStyle(provider.buttonColor)}
                    disabled={isRegistering || isRedirecting || !provider.id}
                    onclick={() => provider.id && loginWithProvider(provider.id)}
                  >
                    {#if isRedirecting && selectedProvider === provider.id}
                      <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                      Redirecting...
                    {:else}
                      <ExternalLink class="mr-2 h-4 w-4" />
                      Sign in with {provider.name}
                    {/if}
                  </Button>
                {/each}
              </div>
            {/if}

            <p class="text-center text-xs text-muted-foreground">
              Already have an account?
              <a
                href="/auth/login?returnUrl=/invite/{data.token}"
                class="underline hover:text-foreground"
              >
                Sign in
              </a>
            </p>
          </div>
        {/if}

        <p class="text-center text-xs text-muted-foreground">
          This invite expires on {new Date(invite.expiresAt).toLocaleDateString()}
        </p>
      </Card.Content>
    {/if}
  </Card.Root>
</div>
