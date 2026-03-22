import winston from "winston";

let instance: winston.Logger | null = null;

export function createLogger(): winston.Logger {
  if (instance) return instance;

  const isDev = process.env.NODE_ENV !== "production";
  instance = winston.createLogger({
    level: process.env.LOG_LEVEL ?? "info",
    format: isDev
      ? winston.format.combine(
          winston.format.timestamp(),
          winston.format.simple(),
        )
      : winston.format.combine(
          winston.format.timestamp(),
          winston.format.errors({ stack: true }),
          winston.format.json(),
        ),
    transports: [new winston.transports.Console()],
  });

  return instance;
}
