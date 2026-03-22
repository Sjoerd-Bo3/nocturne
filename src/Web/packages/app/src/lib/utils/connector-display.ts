import {
  Activity,
  Cloud,
  Database,
  Plug,
  Settings,
  Smartphone,
  Sparkles,
} from "lucide-svelte";
import type { DataSourceStatus } from "$lib/components/settings/DataSourceRow.svelte";
import type { ConnectorStatusDto } from "$lib/api/generated/nocturne-api-client";

export function getCategoryIcon(category: string | undefined) {
  switch (category) {
    case "cgm":
      return Activity;
    case "pump":
      return Database;
    case "aid-system":
      return Settings;
    case "connector":
      return Cloud;
    case "uploader":
      return Smartphone;
    case "demo":
      return Sparkles;
    default:
      return Plug;
  }
}

export function mapConnectorStatus(
  connectorStatus: ConnectorStatusDto
): DataSourceStatus {
  if (connectorStatus.state === "Syncing") return "syncing";
  if (connectorStatus.state === "BackingOff") return "backing-off";
  if (
    connectorStatus.state === "Error" ||
    (!connectorStatus.isHealthy && connectorStatus.state !== "Configured")
  )
    return "error";
  if (connectorStatus.state === "Configured") return "configured";
  if (connectorStatus.state === "Disabled") return "disabled";
  if (connectorStatus.state === "Offline") return "offline";
  return "active";
}
