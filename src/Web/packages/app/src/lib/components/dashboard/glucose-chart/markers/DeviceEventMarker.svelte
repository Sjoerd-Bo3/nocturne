<script lang="ts">
  import { Group } from "layerchart";
  import { DeviceEventIcon } from "$lib/components/icons";
  import type { DeviceEventType } from "$lib/api";

  interface Props {
    xPos: number;
    yPos: number;
    eventType?: DeviceEventType;
    color: string;
    treatmentId?: string;
    onMarkerClick?: (treatmentId: string) => void;
  }

  let { xPos, yPos, eventType, color, treatmentId, onMarkerClick }: Props =
    $props();
</script>

<Group
  x={xPos}
  y={yPos}
  onclick={treatmentId && onMarkerClick
    ? () => onMarkerClick(treatmentId)
    : undefined}
  class={treatmentId && onMarkerClick ? "cursor-pointer" : ""}
>
  <!-- Background circle -->
  <circle
    r="12"
    fill="var(--background)"
    stroke={color}
    stroke-width="2"
    class="opacity-95 {treatmentId && onMarkerClick
      ? 'hover:opacity-100 transition-opacity'
      : ''}"
  />
  <!-- Icon using foreignObject to embed Lucide component -->
  <foreignObject x="-10" y="-10" width="20" height="20">
    <div class="flex items-center justify-center w-full h-full">
      <DeviceEventIcon {eventType} size={16} {color} />
    </div>
  </foreignObject>
</Group>
