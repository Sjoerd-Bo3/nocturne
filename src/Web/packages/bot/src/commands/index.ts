import type { Chat } from "chat";
import { NocturneClient } from "../lib/nocturne-client.js";
import { registerGlucoseCommands } from "./glucose.js";
import { registerAccountCommands } from "./account.js";
import { registerAlertCommands } from "./alerts.js";

export function registerAllCommands(bot: Chat, client: NocturneClient) {
  registerGlucoseCommands(bot, client);
  registerAccountCommands(bot);
  registerAlertCommands(bot, client);
}
