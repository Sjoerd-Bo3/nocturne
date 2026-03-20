<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Plus, Trash2 } from "lucide-svelte";

  interface TargetRangeEntry {
    time: string;
    low: number;
    high: number;
  }

  interface Props {
    label: string;
    unit: string;
    entries: TargetRangeEntry[];
  }

  let { label, unit, entries = $bindable() }: Props = $props();

  function addEntry() {
    entries = [...entries, { time: "00:00", low: 0, high: 0 }];
  }

  function removeEntry(index: number) {
    entries = entries.filter((_, i) => i !== index);
  }
</script>

<div class="space-y-3">
  <div class="flex items-center justify-between">
    <Label>{label}</Label>
    <Button variant="outline" size="sm" onclick={addEntry}>
      <Plus class="h-4 w-4 mr-1" />
      Add
    </Button>
  </div>

  {#if entries.length === 0}
    <p class="text-sm text-muted-foreground">No entries yet. Click Add to create one.</p>
  {:else}
    <div class="space-y-2">
      {#each entries as entry, i}
        <div class="flex items-center gap-2">
          <Input type="time" bind:value={entry.time} class="w-32" />
          <Input type="number" bind:value={entry.low} step={1} min={0} class="w-20" placeholder="Low" />
          <span class="text-xs text-muted-foreground">-</span>
          <Input type="number" bind:value={entry.high} step={1} min={0} class="w-20" placeholder="High" />
          <span class="text-sm text-muted-foreground whitespace-nowrap">{unit}</span>
          {#if entries.length > 1}
            <Button variant="ghost" size="icon" onclick={() => removeEntry(i)}>
              <Trash2 class="h-4 w-4" />
            </Button>
          {/if}
        </div>
      {/each}
    </div>
  {/if}
</div>
