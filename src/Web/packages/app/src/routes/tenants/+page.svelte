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
  import * as Alert from "$lib/components/ui/alert";
  import {
    Building2,
    ArrowRightLeft,
    Loader2,
    AlertTriangle,
    Check,
    Info,
  } from "lucide-svelte";
  import { getMyTenants } from "$api/generated/mytenants.generated.remote";
  import { getMultitenancyInfo } from "$api/generated/metadatas.generated.remote";
  import type { TenantDto, MultitenancyInfo } from "$api";
  import { browser } from "$app/environment";

  // Reactive queries
  const tenantsQuery = $derived(getMyTenants());
  const multitenancyQuery = $derived(getMultitenancyInfo());

  const tenants = $derived((tenantsQuery.current as TenantDto[] | undefined) ?? []);
  const mtInfo = $derived(multitenancyQuery.current as MultitenancyInfo | undefined);
  const loading = $derived(tenantsQuery.isPending || multitenancyQuery.isPending);
  const queryError = $derived(tenantsQuery.error || multitenancyQuery.error);

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
</script>

<div class="container max-w-4xl space-y-6 p-6">
  <div class="flex items-center gap-3">
    <Building2 class="h-8 w-8 text-primary" />
    <div>
      <h1 class="text-2xl font-bold">Tenants</h1>
      <p class="text-muted-foreground">
        Switch between your Nocturne instances
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
    {#if mtInfo && !mtInfo.subdomainResolution}
      <Alert.Root>
        <Info class="h-4 w-4" />
        <Alert.Title>Subdomain resolution not configured</Alert.Title>
        <Alert.Description>
          A base domain must be configured to enable URL-based tenant switching.
        </Alert.Description>
      </Alert.Root>
    {/if}

    {#if tenants.length === 0}
      <Card>
        <CardContent class="flex flex-col items-center justify-center py-12 text-center">
          <Building2 class="h-12 w-12 text-muted-foreground/50 mb-4" />
          <p class="text-muted-foreground">You are not a member of any tenants.</p>
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
                  <CardDescription class="font-mono text-xs">{tenant.slug}</CardDescription>
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
                  Created {tenant.sysCreatedAt ? new Date(tenant.sysCreatedAt).toLocaleDateString() : ""}
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
  {/if}
</div>
