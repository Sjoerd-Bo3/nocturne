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
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Alert from "$lib/components/ui/alert";
  import {
    Building2,
    ArrowRightLeft,
    Loader2,
    AlertTriangle,
    Check,
    Info,
    Plus,
    Lock,
  } from "lucide-svelte";
  import {
    getMyTenants,
    createTenant,
    validateSlug,
  } from "$api/generated/mytenants.generated.remote";
  import { getMultitenancyInfo } from "$api/generated/metadatas.generated.remote";
  import type { TenantDto, MultitenancyInfo } from "$api";
  import { browser } from "$app/environment";

  // Reactive queries
  const tenantsQuery = $derived(getMyTenants());
  const multitenancyQuery = $derived(getMultitenancyInfo());

  const tenants = $derived(
    (tenantsQuery.current as TenantDto[] | undefined) ?? [],
  );
  const mtInfo = $derived(
    multitenancyQuery.current as MultitenancyInfo | undefined,
  );
  const loading = $derived(tenantsQuery.loading || multitenancyQuery.loading);
  const queryError = $derived(tenantsQuery.error || multitenancyQuery.error);

  // Creation form state
  let showCreateForm = $state(false);
  let slug = $state("");
  let displayName = $state("");
  let apiSecret = $state("");
  let creating = $state(false);
  let slugError = $state<string | null>(null);
  let slugValid = $state(false);
  let validating = $state(false);
  let createError = $state<string | null>(null);

  // Debounced slug validation
  let validationTimeout: ReturnType<typeof setTimeout> | null = null;

  function onSlugInput() {
    slugError = null;
    slugValid = false;

    if (validationTimeout) clearTimeout(validationTimeout);

    const value = slug.trim().toLowerCase();
    if (!value || value.length < 3) {
      if (value.length > 0) slugError = "Slug must be at least 3 characters";
      return;
    }

    validating = true;
    validationTimeout = setTimeout(async () => {
      try {
        const result = await validateSlug({ slug: value });
        if (result?.isValid) {
          slugValid = true;
          slugError = null;
        } else {
          slugValid = false;
          slugError = result?.message ?? "Invalid slug";
        }
      } catch {
        slugError = "Could not validate slug";
      } finally {
        validating = false;
      }
    }, 400);
  }

  function getTenantUrl(slug: string): string | null {
    if (!mtInfo?.baseDomain) return null;
    const protocol = browser ? window.location.protocol : "https:";
    return `${protocol}//${slug}.${mtInfo.baseDomain}/`;
  }

  function isCurrent(tenant: TenantDto): boolean {
    return tenant.slug === mtInfo?.currentTenantSlug;
  }

  function switchToTenant(slug: string) {
    const url = getTenantUrl(slug);
    if (browser && url) {
      window.location.href = url;
    }
  }

  async function handleCreate() {
    if (!slugValid || !displayName.trim()) return;
    creating = true;
    createError = null;
    try {
      const newTenant = await createTenant({
        slug: slug.trim().toLowerCase(),
        displayName: displayName.trim(),
        apiSecret: apiSecret.trim() || undefined,
      });
      // Redirect to the new tenant's subdomain
      if (newTenant?.slug) {
        switchToTenant(newTenant.slug);
      }
    } catch (err) {
      createError =
        (err as any)?.message ?? "Failed to create tenant. Please try again.";
    } finally {
      creating = false;
    }
  }

  function resetForm() {
    slug = "";
    displayName = "";
    apiSecret = "";
    slugError = null;
    slugValid = false;
    createError = null;
    showCreateForm = false;
  }

  const canCreate = $derived(
    mtInfo?.subdomainResolution && mtInfo?.allowSelfServiceCreation,
  );
</script>

<div class="container max-w-4xl space-y-6 p-6">
  <div class="flex items-center gap-3">
    <Building2 class="h-8 w-8 text-primary" />
    <div>
      <h1 class="text-2xl font-bold">Tenants</h1>
      <p class="text-muted-foreground">
        Manage and switch between your Nocturne instances
      </p>
    </div>
  </div>

  {#if loading}
    <div class="flex items-center justify-center py-12">
      <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
    </div>
  {:else if queryError}
    <Alert.Root variant="destructive">
      <AlertTriangle class="h-4 w-4" />
      <Alert.Title>Error</Alert.Title>
      <Alert.Description>Failed to load tenants</Alert.Description>
    </Alert.Root>
  {:else}
    <!-- Info banners based on multitenancy state -->
    {#if mtInfo && !mtInfo.subdomainResolution}
      <Alert.Root>
        <Info class="h-4 w-4" />
        <Alert.Title>Multitenancy not configured</Alert.Title>
        <Alert.Description>
          A base domain must be configured to enable URL-based tenant switching
          and tenant creation.
        </Alert.Description>
      </Alert.Root>
    {:else if mtInfo?.subdomainResolution && !mtInfo?.allowSelfServiceCreation && tenants.length <= 1}
      <Alert.Root>
        <Lock class="h-4 w-4" />
        <Alert.Title>Tenant creation managed externally</Alert.Title>
        <Alert.Description>
          New tenants are managed by your service provider. Contact your
          administrator to request a new instance.
        </Alert.Description>
      </Alert.Root>
    {/if}

    <!-- Tenant list -->
    {#if tenants.length === 0}
      <Card>
        <CardContent
          class="flex flex-col items-center justify-center py-12 text-center"
        >
          <Building2 class="h-12 w-12 text-muted-foreground/50 mb-4" />
          <p class="text-muted-foreground">
            You are not a member of any tenants.
          </p>
        </CardContent>
      </Card>
    {:else}
      <div class="grid gap-4 md:grid-cols-2">
        {#each tenants as tenant (tenant.id)}
          {@const current = isCurrent(tenant)}
          {@const url = getTenantUrl(tenant.slug ?? "")}
          <Card class={current ? "border-primary" : ""}>
            <CardHeader class="pb-3">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <CardTitle class="text-lg">{tenant.displayName}</CardTitle>
                  <CardDescription class="font-mono text-xs"
                    >{tenant.slug}</CardDescription
                  >
                </div>
                <div class="flex gap-1.5">
                  {#if current}
                    <Badge variant="default">Current</Badge>
                  {/if}
                  {#if tenant.isDefault}
                    <Badge variant="secondary">Default</Badge>
                  {/if}
                  {#if !tenant.isActive}
                    <Badge variant="destructive">Inactive</Badge>
                  {/if}
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div class="flex items-center justify-between">
                <span class="text-xs text-muted-foreground">
                  Created {tenant.sysCreatedAt
                    ? new Date(tenant.sysCreatedAt).toLocaleDateString()
                    : ""}
                </span>
                {#if current}
                  <Button variant="outline" size="sm" disabled>
                    <Check class="mr-2 h-4 w-4" />
                    Current
                  </Button>
                {:else if url}
                  <Button
                    variant="default"
                    size="sm"
                    disabled={!tenant.isActive}
                    onclick={() => switchToTenant(tenant.slug ?? "")}
                  >
                    <ArrowRightLeft class="mr-2 h-4 w-4" />
                    Switch
                  </Button>
                {:else}
                  <Button variant="outline" size="sm" disabled>
                    <ArrowRightLeft class="mr-2 h-4 w-4" />
                    Switch
                  </Button>
                {/if}
              </div>
            </CardContent>
          </Card>
        {/each}
      </div>
    {/if}

    <!-- Create new tenant -->
    {#if canCreate}
      {#if !showCreateForm}
        <Button
          variant="outline"
          class="w-full"
          onclick={() => (showCreateForm = true)}
        >
          <Plus class="mr-2 h-4 w-4" />
          Create new tenant
        </Button>
      {:else}
        <Card>
          <CardHeader>
            <CardTitle>Create new tenant</CardTitle>
            <CardDescription>
              Set up a new Nocturne instance with its own subdomain
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-4">
            {#if createError}
              <Alert.Root variant="destructive">
                <AlertTriangle class="h-4 w-4" />
                <Alert.Description>{createError}</Alert.Description>
              </Alert.Root>
            {/if}

            <div class="space-y-2">
              <Label for="slug">Subdomain</Label>
              <div class="flex items-center gap-2">
                <Input
                  id="slug"
                  bind:value={slug}
                  oninput={onSlugInput}
                  placeholder="my-instance"
                  class="font-mono {slugError
                    ? 'border-destructive'
                    : slugValid
                      ? 'border-green-500'
                      : ''}"
                />
                {#if mtInfo?.baseDomain}
                  <span class="text-sm text-muted-foreground whitespace-nowrap">
                    .{mtInfo.baseDomain.split(":")[0]}
                  </span>
                {/if}
              </div>
              {#if validating}
                <p class="text-xs text-muted-foreground">Checking availability...</p>
              {:else if slugError}
                <p class="text-xs text-destructive">{slugError}</p>
              {:else if slugValid}
                <p class="text-xs text-green-600">Available</p>
              {/if}
            </div>

            <div class="space-y-2">
              <Label for="displayName">Display name</Label>
              <Input
                id="displayName"
                bind:value={displayName}
                placeholder="My Nocturne Instance"
              />
            </div>

            <div class="space-y-2">
              <Label for="apiSecret">API secret (optional)</Label>
              <Input
                id="apiSecret"
                bind:value={apiSecret}
                type="password"
                placeholder="For Nightscout compatibility"
              />
              <p class="text-xs text-muted-foreground">
                Only needed for legacy Nightscout client compatibility
              </p>
            </div>

            <div class="flex gap-2 justify-end">
              <Button variant="outline" onclick={resetForm}>Cancel</Button>
              <Button
                onclick={handleCreate}
                disabled={creating || !slugValid || !displayName.trim()}
              >
                {#if creating}
                  <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                {/if}
                Create tenant
              </Button>
            </div>
          </CardContent>
        </Card>
      {/if}
    {/if}
  {/if}
</div>
