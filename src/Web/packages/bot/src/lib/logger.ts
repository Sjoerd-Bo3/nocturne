import winston from "winston";

export function createLogger() {
  const isDev = process.env.NODE_ENV !== "production";
  return winston.createLogger({
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
}
