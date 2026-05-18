<script lang="ts">
  import type { BrowserAlarmCapabilities } from "$lib/audio/alarm-sounds";

  interface Props {
    capabilities: BrowserAlarmCapabilities | null;
    feature: "notifications" | "vibration" | "wakeLock";
  }

  let { capabilities, feature }: Props = $props();
</script>

{#if capabilities && !capabilities[feature]}
  <span
    class="px-1.5 py-0.5 text-[10px] font-medium rounded bg-muted text-muted-foreground"
  >
    Not on this device
  </span>
{:else if feature === "notifications" && capabilities && capabilities.notificationPermission === "denied"}
  <span
    class="px-1.5 py-0.5 text-[10px] font-medium rounded bg-red-500/10 text-red-500"
  >
    Blocked on this device
  </span>
{:else if feature === "notifications" && capabilities && capabilities.notificationPermission === "default"}
  <span
    class="px-1.5 py-0.5 text-[10px] font-medium rounded bg-yellow-500/10 text-yellow-600 dark:text-yellow-400"
  >
    Needs Permission
  </span>
{/if}
