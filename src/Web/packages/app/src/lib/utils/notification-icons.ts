import {
  AlertTriangle,
  CircleDot,
  Info,
  Clock,
  Bell,
  Timer,
  Settings2,
  HelpCircle,
  User,
  TrendingDown,
  Utensils,
  RefreshCw,
  WifiOff,
  Gift,
  Activity,
  Zap,
  Shield,
  Database,
  Link,
  MessageSquare,
  Calendar,
  Heart,
  Thermometer,
  Droplets,
  BatteryWarning,
  CloudOff,
  AlertCircle,
  CheckCircle,
} from "lucide-svelte";
import { NotificationCategory } from "$lib/api/generated/nocturne-api-client";
import type { ComponentType } from "svelte";

const ICON_MAP: Record<string, ComponentType> = {
  "alert-triangle": AlertTriangle,
  "circle-dot": CircleDot,
  info: Info,
  clock: Clock,
  bell: Bell,
  timer: Timer,
  "settings-2": Settings2,
  "help-circle": HelpCircle,
  user: User,
  "trending-down": TrendingDown,
  utensils: Utensils,
  "refresh-cw": RefreshCw,
  "wifi-off": WifiOff,
  gift: Gift,
  activity: Activity,
  zap: Zap,
  shield: Shield,
  database: Database,
  link: Link,
  "message-square": MessageSquare,
  calendar: Calendar,
  heart: Heart,
  thermometer: Thermometer,
  droplets: Droplets,
  "battery-warning": BatteryWarning,
  "cloud-off": CloudOff,
  "alert-circle": AlertCircle,
  "check-circle": CheckCircle,
};

const CATEGORY_DEFAULTS: Record<string, ComponentType> = {
  [NotificationCategory.Alert]: AlertTriangle,
  [NotificationCategory.ActionRequired]: CircleDot,
  [NotificationCategory.Informational]: Info,
  [NotificationCategory.Reminder]: Clock,
};

export function resolveNotificationIcon(
  iconName: string | undefined,
  category: NotificationCategory | undefined,
): ComponentType {
  if (iconName && ICON_MAP[iconName]) {
    return ICON_MAP[iconName];
  }
  if (category && CATEGORY_DEFAULTS[category]) {
    return CATEGORY_DEFAULTS[category];
  }
  return Bell;
}
