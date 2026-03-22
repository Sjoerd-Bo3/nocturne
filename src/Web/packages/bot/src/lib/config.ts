export interface BotConfig {
  apiUrl: string;
  platforms: {
    discord: boolean;
    slack: boolean;
    telegram: boolean;
    whatsapp: boolean;
  };
  postgres: {
    connectionString: string;
  };
}

export function loadConfig(): BotConfig {
  const apiUrl = process.env.API_URL;
  if (!apiUrl) throw new Error("API_URL is required");

  return {
    apiUrl,
    platforms: {
      discord: !!process.env.DISCORD_TOKEN,
      slack: !!process.env.SLACK_BOT_TOKEN && !!process.env.SLACK_SIGNING_SECRET,
      telegram: !!process.env.TELEGRAM_BOT_TOKEN,
      whatsapp: !!process.env.WHATSAPP_ACCESS_TOKEN,
    },
    postgres: {
      connectionString:
        process.env.DATABASE_CONNECTION_STRING ??
        process.env.ConnectionStrings__nocturne_postgres ??
        "",
    },
  };
}
