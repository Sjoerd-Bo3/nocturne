import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import { createLogger } from "../lib/logger.js";
import type { AlertPayload } from "../lib/nocturne-client.js";

const logger = createLogger();

export interface AlertDispatchEvent {
  deliveryId: string;
  channelType: string;
  destination: string;
  payload: AlertPayload;
}

export interface AlertAckEvent {
  tenantId: string;
  acknowledgedBy: string;
}

export interface AlertResolvedEvent {
  tenantId: string;
  excursionId: string;
  resolvedValue?: number;
}

export interface AlertListenerHandlers {
  onDispatch: (event: AlertDispatchEvent) => Promise<void>;
  onAcknowledged: (event: AlertAckEvent) => Promise<void>;
  onResolved: (event: AlertResolvedEvent) => Promise<void>;
}

export class AlertListener {
  private connection: HubConnection | null = null;
  private hubUrl: string;
  private handlers: AlertListenerHandlers;
  private reconnectDelay = 5000;
  private maxReconnectDelay = 30000;

  constructor(hubUrl: string, handlers: AlertListenerHandlers) {
    this.hubUrl = hubUrl;
    this.handlers = handlers;
  }

  async connect(): Promise<void> {
    this.connection = new HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) =>
          Math.min(
            this.reconnectDelay * Math.pow(2, ctx.previousRetryCount),
            this.maxReconnectDelay,
          ),
      })
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.on("alert_dispatch", async (data: AlertDispatchEvent) => {
      logger.info(`Alert dispatch received: ${data.payload.ruleName}`);
      try {
        await this.handlers.onDispatch(data);
      } catch (err) {
        logger.error("Error handling alert dispatch:", err);
      }
    });

    this.connection.on("alert_acknowledged", async (data: AlertAckEvent) => {
      logger.info(`Alert acknowledged by ${data.acknowledgedBy}`);
      try {
        await this.handlers.onAcknowledged(data);
      } catch (err) {
        logger.error("Error handling alert ack:", err);
      }
    });

    this.connection.on("alert_resolved", async (data: AlertResolvedEvent) => {
      logger.info(`Alert resolved for excursion ${data.excursionId}`);
      try {
        await this.handlers.onResolved(data);
      } catch (err) {
        logger.error("Error handling alert resolution:", err);
      }
    });

    this.connection.onreconnected(async () => {
      logger.info("AlertHub reconnected, re-subscribing...");
      await this.subscribe();
    });

    this.connection.onclose(() => {
      logger.warn("AlertHub connection closed");
    });

    await this.connection.start();
    logger.info("AlertHub connection established");
    await this.subscribe();
  }

  private async subscribe(): Promise<void> {
    if (!this.connection) return;
    await this.connection.invoke("Subscribe");
    logger.info("Subscribed to alert events");
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      logger.info("AlertHub connection stopped");
    }
  }

  isConnected(): boolean {
    return this.connection?.state === "Connected";
  }
}
