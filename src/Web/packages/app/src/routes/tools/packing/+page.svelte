<script lang="ts">
  import { Card, CardContent } from "$lib/components/ui/card";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import {
    Luggage,
    Syringe,
    Activity,
    Cpu,
    TestTube,
    ShieldAlert,
    Info,
  } from "lucide-svelte";
  import SupplyCategory from "$lib/components/tools/packing/supply-category.svelte";
  import { categories } from "$lib/components/tools/packing/packing-config";

  const { data } = $props();
  let tripDays = $state(7);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const iconMap: Record<string, any> = {
    Syringe,
    Activity,
    Cpu,
    TestTube,
    ShieldAlert,
  };
</script>

<div class="container mx-auto p-6 max-w-3xl space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-2xl font-bold tracking-tight flex items-center gap-2">
      <Luggage class="h-6 w-6" />
      Packing Calculator
    </h1>
    <p class="text-muted-foreground">
      Calculate the supplies you need for your trip.
    </p>
  </div>

  <!-- Trip Duration -->
  <Card>
    <CardContent class="pt-6">
      <div class="flex items-center gap-4">
        <Label class="text-base font-medium whitespace-nowrap">Trip duration</Label>
        <Input
          type="number"
          bind:value={tripDays}
          min={1}
          max={365}
          step={1}
          class="w-24"
        />
        <span class="text-muted-foreground">days</span>
      </div>
    </CardContent>
  </Card>

  <!-- TDD Hint -->
  {#if data.avgTdd}
    <div class="flex items-start gap-2 rounded-lg border border-border bg-muted/50 p-4 text-sm">
      <Info class="h-4 w-4 mt-0.5 text-muted-foreground shrink-0" />
      <p class="text-muted-foreground">
        Based on your last 14 days, your average daily insulin use is
        <span class="font-semibold text-foreground">~{data.avgTdd}u</span>.
      </p>
    </div>
  {/if}

  <!-- Category Cards -->
  {#each categories as category}
    <SupplyCategory
      config={category}
      icon={iconMap[category.icon]}
      {tripDays}
      avgTdd={data.avgTdd}
      eventIntervals={data.eventIntervals}
    />
  {/each}
</div>
