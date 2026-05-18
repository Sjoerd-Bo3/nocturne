<script lang="ts">
  interface Props {
    activeDays: number[] | undefined;
  }

  let { activeDays = $bindable() }: Props = $props();

  const daysOfWeek = [
    { value: 0, label: "Sun" },
    { value: 1, label: "Mon" },
    { value: 2, label: "Tue" },
    { value: 3, label: "Wed" },
    { value: 4, label: "Thu" },
    { value: 5, label: "Fri" },
    { value: 6, label: "Sat" },
  ];

  function toggleDay(day: number) {
    const days = activeDays ?? [];
    const index = days.indexOf(day);
    if (index >= 0) {
      activeDays = days.filter((d) => d !== day);
    } else {
      activeDays = [...days, day];
    }
  }
</script>

<div class="flex gap-2">
  {#each daysOfWeek as day}
    {@const isActive =
      activeDays?.includes(day.value) ??
      (activeDays === undefined || activeDays.length === 0)}
    <button
      class="px-3 py-2 rounded-lg text-sm font-medium transition-colors
        {isActive
        ? 'bg-primary text-primary-foreground'
        : 'bg-muted hover:bg-muted/80'}"
      onclick={() => toggleDay(day.value)}
    >
      {day.label}
    </button>
  {/each}
</div>
