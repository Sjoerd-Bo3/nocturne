import { createBot } from "./bot.js";
import { createLogger } from "./lib/logger.js";
import { AlertListener } from "./alerts/listener.js";
import { AlertDeliveryHandler } from "./alerts/deliver.js";

const logger = createLogger();

async function main() {
  logger.info("Starting Nocturne bot...");

  const { bot, client, config } = createBot();
  await bot.initialize();

  logger.info("Nocturne bot started successfully");

  // Start alert listener
  const alertHubUrl = `${config.apiUrl}/hubs/alerts`;
  const deliveryHandler = new AlertDeliveryHandler(bot, client);

  const alertListener = new AlertListener(alertHubUrl, {
    onDispatch: (event) => deliveryHandler.deliver(event),
    onAcknowledged: async (event) => {
      // TODO: edit previously sent messages to show "Acknowledged by {name}"
      logger.info(`Acknowledged event for tenant ${event.tenantId} by ${event.acknowledgedBy}`);
    },
    onResolved: async (event) => {
      // TODO: edit previously sent messages to show "Resolved"
      logger.info(`Resolved event for excursion ${event.excursionId}`);
    },
  });

  try {
    await alertListener.connect();
  } catch (err) {
    logger.error("Failed to connect to AlertHub (will retry on next cycle):", err);
  }

  // Heartbeat -- report active platforms every 30s
  const activePlatforms = Object.entries(config.platforms)
    .filter(([, enabled]) => enabled)
    .map(([name]) => name);

  const heartbeatInterval = setInterval(() => {
    client.sendHeartbeat(activePlatforms).catch((err) =>
      logger.error("Heartbeat failed:", err),
    );
  }, 30_000);

  // Startup recovery -- process any pending deliveries
  try {
    const channelTypes = activePlatforms.flatMap((p) => [`${p}_dm`, `${p}_channel`]);
    const pending = await client.getPendingDeliveries(channelTypes);
    if (pending.length > 0) {
      logger.info(`Processing ${pending.length} pending deliveries from startup recovery`);
      for (const delivery of pending) {
        await deliveryHandler.deliver(delivery).catch((err) =>
          logger.error(`Startup recovery delivery failed:`, err),
        );
      }
    }
  } catch (err) {
    logger.error("Startup recovery failed:", err);
  }

  // Graceful shutdown
  const shutdown = async () => {
    logger.info("Shutting down Nocturne bot...");
    clearInterval(heartbeatInterval);
    await alertListener.disconnect();
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
