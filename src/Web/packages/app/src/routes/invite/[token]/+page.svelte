<script lang="ts">
  import { page } from "$app/state";
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
    Fingerprint,
    ExternalLink,
    Loader2,
    Copy,
    ShieldCheck,
    Shield,
    ChevronDown,
    ChevronUp,
  } from "lucide-svelte";
  import { startRegistration } from "@simplewebauthn/browser";
  import { getOidcProviders, setAuthCookies } from "$routes/auth/auth.remote";
  import {
    inviteOptions,
    inviteComplete,
  } from "$lib/api/generated/passkeys.generated.remote";
  import {
    getInviteInfo,
    acceptInvite,
  } from "$lib/api/generated/memberinvites.generated.remote";
  import { getRoles } from "$lib/api/generated/roles.generated.remote";

  const token = $derived(page.params.token);
  const isAuthenticated = $derived(page.data.isAuthenticated);

  const inviteQuery = $derived(getInviteInfo(token ?? ""));
  const invite = $derived(inviteQuery.current);
  const isLoading = $derived(!inviteQuery.current && !inviteQuery.error);

  // Fetch roles to resolve roleIds to names/permissions
  const rolesQuery = $derived(getRoles());
  const allRoles = $derived(rolesQuery.current ?? []);

  /** Permission categories for read-only display (mirrors PermissionPicker) */
  const permissionLabels: Record<string, string> = {
    "entries.read": "Read blood glucose",
    "entries.readwrite": "Read & write blood glucose",
    "treatments.read": "Read treatments",
    "treatments.readwrite": "Read & write treatments",
    "devicestatus.read": "Read device status",
    "devicestatus.readwrite": "Read & write device status",
    "profile.read": "Read profile",
    "profile.readwrite": "Read & write profile",
    "notifications.read": "Read notifications",
    "notifications.readwrite": "Read & write notifications",
    "reports.read": "Read reports",
    "health.read": "Read health data",
    "identity.read": "Read identity",
    "roles.manage": "Manage roles",
    "members.invite": "Invite members",
    "members.manage": "Manage members",
    "tenant.settings": "Tenant settings",
    "sharing.manage": "Manage sharing",
    "*": "Full access",
  };

  // Resolve invite roles from roleIds
  const inviteRoles = $derived(
    (invite?.roleIds ?? [])
      .map((id: string) => allRoles.find((r) => r.id === id))
      .filter((r): r is NonNullable<typeof r> => !!r),
  );

  const hasRoles = $derived(inviteRoles.length > 0);
  const hasDirectPermissions = $derived(
    (invite?.directPermissions ?? []).length > 0,
  );

  // Track which role's permissions are expanded
  let expandedRoleId = $state<string | null>(null);

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
  let isAccepting = $state(false);

  const canRegister = $derived(
    username.trim().length > 0 && displayName.trim().length > 0,
  );

  async function handleAcceptInvite() {
    isAccepting = true;
    errorMessage = null;
    try {
      await acceptInvite(token ?? "");
      await goto("/", { invalidateAll: true });
    } catch (err) {
      errorMessage =
        err instanceof Error ? err.message : "Failed to accept invite.";
    } finally {
      isAccepting = false;
    }
  }

  async function handlePasskeyRegistration() {
    if (!token) return;

    isRegistering = true;
    errorMessage = null;

    try {
      const optionsResult = await inviteOptions({
        token,
        username: username.trim(),
        displayName: displayName.trim(),
      });

      const options = JSON.parse(optionsResult.options ?? "");
      const attestation = await startRegistration({ optionsJSON: options });

      const result = await inviteComplete({
        token,
        attestationResponseJson: JSON.stringify(attestation),
        challengeToken: optionsResult.challengeToken ?? "",
      });

      // Set auth cookies so the user is logged in immediately
      if (result.accessToken) {
        await setAuthCookies({
          accessToken: result.accessToken,
          refreshToken: result.refreshToken ?? undefined,
          expiresIn: result.expiresIn ?? undefined,
        });
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
    params.set("returnUrl", `/invite/${token}`);

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
    {#if isLoading}
      <Card.Header class="text-center">
        <div
          class="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-muted"
        >
          <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
        <Card.Title class="text-xl">Loading invite...</Card.Title>
      </Card.Header>
    {:else if !invite}
      <!-- Invite not found -->
      <Card.Header class="text-center">
        <div
          class="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-destructive/10"
        >
          <AlertTriangle class="h-8 w-8 text-destructive" />
        </div>
        <Card.Title class="text-xl">Invite Not Found</Card.Title>
        <Card.Description>
          This invite link is invalid or has expired.
        </Card.Description>
      </Card.Header>
      <Card.Content class="text-center">
        <Button href="/auth/login" variant="outline"> Go to Login </Button>
      </Card.Content>
    {:else if !invite.isValid}
      <!-- Invite expired or revoked -->
      <Card.Header class="text-center">
        <div
          class="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-muted"
        >
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
            This invite link has expired. Please ask {invite.createdByName ??
              "the invite creator"} for a new invite.
          {:else if invite.isRevoked}
            This invite link has been revoked by {invite.createdByName ??
              "the invite creator"}.
          {:else}
            This invite link is no longer available.
          {/if}
        </Card.Description>
      </Card.Header>
      <Card.Content class="text-center">
        <Button href="/auth/login" variant="outline"> Go to Login </Button>
      </Card.Content>
    {:else}
      <!-- Valid invite -->
      <Card.Header class="text-center">
        <div
          class="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-primary/10"
        >
          <UserPlus class="h-8 w-8 text-primary" />
        </div>
        <Card.Title class="text-xl">You're Invited</Card.Title>
        <Card.Description>
          You've been invited to join
          <span class="font-medium text-foreground"
            >{invite.tenantName ?? "a Nocturne site"}</span
          >
          {#if invite.label}
            <Badge variant="secondary" class="ml-2">{invite.label}</Badge>
          {/if}
        </Card.Description>
      </Card.Header>

      <Card.Content class="space-y-6">
        {#if hasRoles}
          <div class="space-y-3">
            <p class="text-sm font-medium">This invite grants the following roles:</p>
            {#each inviteRoles as role (role.id)}
              <div class="rounded-md border bg-muted/50 overflow-hidden">
                <button
                  type="button"
                  class="flex items-center justify-between w-full p-3 text-left hover:bg-muted/80 transition-colors"
                  onclick={() => expandedRoleId = expandedRoleId === (role.id ?? null) ? null : (role.id ?? null)}
                >
                  <div class="flex items-center gap-2">
                    <Shield class="h-4 w-4 text-primary" />
                    <span class="text-sm font-medium">{role.name}</span>
                    {#if role.permissions?.length}
                      <Badge variant="outline" class="text-xs">
                        {role.permissions.length} permission{role.permissions.length !== 1 ? "s" : ""}
                      </Badge>
                    {/if}
                  </div>
                  {#if expandedRoleId === role.id}
                    <ChevronUp class="h-4 w-4 text-muted-foreground" />
                  {:else}
                    <ChevronDown class="h-4 w-4 text-muted-foreground" />
                  {/if}
                </button>
                {#if expandedRoleId === role.id && role.permissions?.length}
                  <div class="border-t px-3 py-2 space-y-1">
                    {#each role.permissions as perm}
                      <div class="flex items-center gap-2 text-sm text-muted-foreground">
                        <Check class="h-3 w-3 text-primary shrink-0" />
                        <span>{permissionLabels[perm] ?? perm}</span>
                      </div>
                    {/each}
                  </div>
                {/if}
              </div>
            {/each}
          </div>
        {/if}

        {#if hasDirectPermissions}
          <div class="space-y-2">
            <p class="text-sm font-medium">Direct Permissions</p>
            <div class="rounded-md border bg-muted/50 p-3 space-y-1">
              {#each invite.directPermissions ?? [] as perm}
                <div class="flex items-center gap-2 text-sm text-muted-foreground">
                  <Check class="h-3 w-3 text-primary shrink-0" />
                  <span>{permissionLabels[perm] ?? perm}</span>
                </div>
              {/each}
            </div>
          </div>
        {/if}

        {#if invite.limitTo24Hours}
          <p class="text-xs text-muted-foreground">
            Access is limited to the most recent 24 hours of data.
          </p>
        {/if}

        {#if errorMessage}
          <div
            class="flex items-start gap-3 rounded-md border border-destructive/20 bg-destructive/5 p-3"
          >
            <AlertTriangle
              class="mt-0.5 h-4 w-4 shrink-0 text-destructive"
            />
            <p class="text-sm text-destructive">{errorMessage}</p>
          </div>
        {/if}

        {#if isAuthenticated}
          <!-- User is logged in - show accept button -->
          <Button
            class="w-full"
            size="lg"
            disabled={isAccepting}
            onclick={handleAcceptInvite}
          >
            {#if isAccepting}
              <Loader2 class="mr-2 h-4 w-4 animate-spin" />
              Accepting...
            {:else}
              <Check class="mr-2 h-4 w-4" />
              Accept Invite
            {/if}
          </Button>
        {:else if registrationComplete}
          <!-- Registration complete - show recovery codes -->
          <div class="space-y-4">
            <div
              class="flex items-start gap-3 rounded-md border border-green-500/20 bg-green-500/5 p-3"
            >
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
                  Save these recovery codes in a safe place. Each code can only
                  be used once.
                </p>

                <div
                  class="grid grid-cols-2 gap-2 rounded-lg border bg-muted/50 p-4"
                >
                  {#each recoveryCodes as code}
                    <code
                      class="rounded bg-background px-2 py-1 text-center text-sm font-mono"
                    >
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
                <div
                  class="relative flex justify-center text-xs uppercase"
                >
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
                    disabled={isRegistering ||
                      isRedirecting ||
                      !provider.id}
                    onclick={() =>
                      provider.id && loginWithProvider(provider.id)}
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
                href="/auth/login?returnUrl=/invite/{token}"
                class="underline hover:text-foreground"
              >
                Sign in
              </a>
            </p>
          </div>
        {/if}

        <p class="text-center text-xs text-muted-foreground">
          This invite expires on {invite.expiresAt
            ? new Date(invite.expiresAt).toLocaleDateString()
            : "unknown"}
        </p>
      </Card.Content>
    {/if}
  </Card.Root>
</div>
