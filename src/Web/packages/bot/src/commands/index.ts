import type { Chat } from "chat";
import type { BotApiClient } from "../types.js";
import { registerGlucoseCommands } from "./glucose.js";
import { registerAccountCommands } from "./account.js";
import { registerAlertCommands } from "./alerts.js";

export function registerAllCommands(bot: Chat, api: BotApiClient) {
  registerGlucoseCommands(bot, api);
  registerAccountCommands(bot);
  registerAlertCommands(bot, api);
}
