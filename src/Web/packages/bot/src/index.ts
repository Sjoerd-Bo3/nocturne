import { createBot } from "./bot.js";
import { createLogger } from "./lib/logger.js";

const logger = createLogger();

async function main() {
  logger.info("Starting Nocturne bot...");

  const { bot, client, config } = createBot();
  await bot.initialize();

  logger.info("Nocturne bot started successfully");

  const shutdown = async () => {
    logger.info("Shutting down Nocturne bot...");
    await bot.shutdown();
    process.exit(0);
  };

  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}

main().catch((err) => {
  logger.error("Fatal error starting bot:", err);
  process.exit(1);
});
