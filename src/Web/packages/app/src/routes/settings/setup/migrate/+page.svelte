<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import { ChevronRight, Sparkles, ArrowRightLeft } from "lucide-svelte";
</script>

<svelte:head>
  <title>Migration - Setup - Nocturne</title>
</svelte:head>

<div class="container mx-auto max-w-4xl p-6 space-y-6">
  <div>
    <h1 class="text-2xl font-bold tracking-tight">Get Started</h1>
    <p class="text-muted-foreground">
      Choose how you'd like to set up Nocturne
    </p>
  </div>

  <div class="grid gap-4 sm:grid-cols-2">
    <a href="/settings/setup" class="block">
      <Card.Root
        class="h-full transition-colors hover:bg-muted/50 hover:border-primary/30"
      >
        <Card.Header class="space-y-4 p-6">
          <div
            class="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10 text-primary"
          >
            <Sparkles class="h-6 w-6" />
          </div>
          <div>
            <Card.Title class="text-lg font-semibold">Starting Fresh</Card.Title>
            <Card.Description class="mt-1">
              Set up Nocturne from scratch with a step-by-step wizard
            </Card.Description>
          </div>
        </Card.Header>
        <Card.Content class="pt-0 px-6 pb-6">
          <span
            class="inline-flex items-center gap-1 text-sm text-primary font-medium"
          >
            Begin setup
            <ChevronRight class="h-4 w-4" />
          </span>
        </Card.Content>
      </Card.Root>
    </a>

    <a href="/settings/setup/migrate/nightscout" class="block">
      <Card.Root
        class="h-full transition-colors hover:bg-muted/50 hover:border-primary/30"
      >
        <Card.Header class="space-y-4 p-6">
          <div
            class="flex h-12 w-12 items-center justify-center rounded-lg bg-blue-500/10 text-blue-600"
          >
            <ArrowRightLeft class="h-6 w-6" />
          </div>
          <div>
            <Card.Title class="text-lg font-semibold"
              >Migrating from Nightscout</Card.Title
            >
            <Card.Description class="mt-1">
              Copy your existing data and keep both systems in sync during
              transition
            </Card.Description>
          </div>
        </Card.Header>
        <Card.Content class="pt-0 px-6 pb-6">
          <span
            class="inline-flex items-center gap-1 text-sm text-blue-600 font-medium"
          >
            Start migration
            <ChevronRight class="h-4 w-4" />
          </span>
        </Card.Content>
      </Card.Root>
    </a>
  </div>
</div>
