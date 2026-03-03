<script lang="ts">
  import { Input } from "$lib/components/ui/input";
  import { Switch } from "$lib/components/ui/switch";
  import { Label } from "$lib/components/ui/label";
  import { Badge } from "$lib/components/ui/badge";
  import type { SupplyItemConfig } from "./packing-config";

  interface Props {
    config: SupplyItemConfig;
    tripDays: number;
    avgTdd: number | null;
    hintInterval: number | null;
  }

  const { config, tripDays, avgTdd, hintInterval }: Props = $props();

  let enabled = $state(config.defaultEnabled);
  let interval = $state(config.defaultInterval ?? 0);
  let buffer = $state(config.defaultBuffer);
  let containerSize = $state(config.defaultContainerSize ?? 300);
  let flatQuantity = $state(1);
  let overrideQuantity = $state<number | null>(null);

  // Apply hint interval when available
  $effect(() => {
    if (hintInterval != null && config.mode === "interval") {
      interval = Math.round(hintInterval * 10) / 10;
    }
  });

  const autoQuantity = $derived.by(() => {
    if (tripDays <= 0) return 0;

    if (config.mode === "interval") {
      if (interval <= 0) return 0;
      return Math.ceil(tripDays / interval) + buffer;
    }

    if (config.mode === "insulin") {
      if (!avgTdd || avgTdd <= 0 || containerSize <= 0) return 0;
      const totalInsulin = avgTdd * tripDays * (1 + buffer);
      return Math.ceil(totalInsulin / containerSize);
    }

    // flat mode
    return flatQuantity;
  });

  const quantity = $derived(overrideQuantity ?? autoQuantity);

  const insulinTotal = $derived.by(() => {
    if (config.mode !== "insulin") return null;
    return quantity * containerSize;
  });

  function clearOverride() {
    overrideQuantity = null;
  }
</script>

<div class="flex flex-col gap-2 py-3 {!enabled ? 'opacity-50' : ''}">
  <div class="flex items-center gap-3">
    <Switch bind:checked={enabled} />
    <Label class="flex-1 font-medium">{config.label}</Label>
    <div class="flex items-center gap-2">
      {#if overrideQuantity != null}
        <Badge variant="outline" class="text-xs cursor-pointer" onclick={clearOverride}>
          Manual
        </Badge>
      {/if}
      <span class="text-lg font-semibold tabular-nums w-8 text-right">
        {enabled ? quantity : 0}
      </span>
    </div>
  </div>

  {#if enabled}
    <div class="ml-12 flex flex-wrap gap-3 text-sm">
      {#if config.mode === "interval"}
        <div class="flex items-center gap-1.5">
          <Label class="text-muted-foreground text-xs whitespace-nowrap">Every</Label>
          <Input
            type="number"
            bind:value={interval}
            min={0.5}
            step={0.5}
            class="w-16 h-7 text-xs"
          />
          <span class="text-muted-foreground text-xs">days</span>
        </div>
        {#if hintInterval != null}
          <span class="text-muted-foreground text-xs self-center">
            (avg: {hintInterval}d)
          </span>
        {/if}
        <div class="flex items-center gap-1.5">
          <Label class="text-muted-foreground text-xs">+</Label>
          <Input
            type="number"
            bind:value={buffer}
            min={0}
            step={1}
            class="w-14 h-7 text-xs"
          />
          <span class="text-muted-foreground text-xs">extra</span>
        </div>
      {:else if config.mode === "insulin"}
        <div class="flex items-center gap-1.5">
          <Label class="text-muted-foreground text-xs whitespace-nowrap">Container</Label>
          <Input
            type="number"
            bind:value={containerSize}
            min={1}
            step={10}
            class="w-20 h-7 text-xs"
          />
          <span class="text-muted-foreground text-xs">units</span>
        </div>
        <div class="flex items-center gap-1.5">
          <Label class="text-muted-foreground text-xs">Buffer</Label>
          <Input
            type="number"
            bind:value={buffer}
            min={0}
            max={2}
            step={0.1}
            class="w-16 h-7 text-xs"
          />
          <span class="text-muted-foreground text-xs">
            ({Math.round(buffer * 100)}%)
          </span>
        </div>
        {#if avgTdd}
          <span class="text-muted-foreground text-xs self-center">
            (TDD: ~{avgTdd}u/day)
          </span>
        {/if}
        {#if insulinTotal}
          <span class="text-muted-foreground text-xs self-center">
            = {insulinTotal}u total
          </span>
        {/if}
      {:else}
        <!-- flat mode -->
        <div class="flex items-center gap-1.5">
          <Label class="text-muted-foreground text-xs">Qty</Label>
          <Input
            type="number"
            bind:value={flatQuantity}
            min={0}
            step={1}
            class="w-16 h-7 text-xs"
          />
        </div>
      {/if}

      <div class="flex items-center gap-1.5 ml-auto">
        <Label class="text-muted-foreground text-xs">Override</Label>
        <Input
          type="number"
          value={overrideQuantity ?? ""}
          oninput={(e) => {
            const v = e.currentTarget.value;
            overrideQuantity = v === "" ? null : parseInt(v, 10);
          }}
          min={0}
          step={1}
          placeholder="auto"
          class="w-16 h-7 text-xs"
        />
      </div>
    </div>
  {/if}
</div>
