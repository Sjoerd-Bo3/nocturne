<script lang="ts">
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import * as Collapsible from "$lib/components/ui/collapsible";
  import { Badge } from "$lib/components/ui/badge";
  import { Separator } from "$lib/components/ui/separator";
  import { ChevronDown, ChevronRight } from "lucide-svelte";
  import SupplyItem from "./supply-item.svelte";
  import type { SupplyCategoryConfig } from "./packing-config";
  import type { Component } from "svelte";

  interface Props {
    config: SupplyCategoryConfig;
    icon: Component<{ class?: string }>;
    tripDays: number;
    avgTdd: number | null;
    eventIntervals: Record<string, number>;
  }

  const { config, icon: Icon, tripDays, avgTdd, eventIntervals }: Props = $props();

  let expanded = $state(true);

  function getHintInterval(item: SupplyCategoryConfig["items"][number]): number | null {
    if (!item.hintEventTypes) return null;
    for (const eventType of item.hintEventTypes) {
      if (eventIntervals[eventType] != null) {
        return eventIntervals[eventType];
      }
    }
    return null;
  }
</script>

<Collapsible.Root bind:open={expanded}>
  <Card>
    <Collapsible.Trigger class="w-full">
      <CardHeader class="cursor-pointer hover:bg-muted/50 transition-colors">
        <div class="flex items-center justify-between">
          <CardTitle class="flex items-center gap-2 text-base">
            <div class="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10">
              <Icon class="h-4 w-4 text-primary" />
            </div>
            {config.label}
            <Badge variant="secondary" class="text-xs font-normal">
              {config.items.length}
            </Badge>
          </CardTitle>
          {#if expanded}
            <ChevronDown class="h-4 w-4 text-muted-foreground" />
          {:else}
            <ChevronRight class="h-4 w-4 text-muted-foreground" />
          {/if}
        </div>
      </CardHeader>
    </Collapsible.Trigger>
    <Collapsible.Content>
      <CardContent class="pt-0">
        {#each config.items as item, i}
          {#if i > 0}
            <Separator />
          {/if}
          <SupplyItem
            config={item}
            {tripDays}
            {avgTdd}
            hintInterval={getHintInterval(item)}
          />
        {/each}
      </CardContent>
    </Collapsible.Content>
  </Card>
</Collapsible.Root>
