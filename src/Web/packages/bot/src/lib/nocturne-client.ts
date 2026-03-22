import { createLogger } from "./logger.js";

const logger = createLogger();

export interface GlucoseReading {
  sgv: number;
  direction: string;
  date: number;
  dateString: string;
  delta?: number;
}

export interface AlertPayload {
  alertType: string;
  ruleName: string;
  glucoseValue: number | null;
  trend: string | null;
  trendRate: number | null;
  readingTimestamp: string;
  excursionId: string;
  instanceId: string;
  tenantId: string;
  subjectName: string;
  activeExcursionCount: number;
}

export class NocturneClient {
  private baseUrl: string;
  private serviceToken: string;

  constructor(baseUrl: string, serviceToken: string = "") {
    this.baseUrl = baseUrl.replace(/\/$/, "");
    this.serviceToken = serviceToken;
  }

  private async request<T>(
    path: string,
    options: RequestInit & { token?: string } = {},
  ): Promise<T> {
    const { token, ...fetchOptions } = options;
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...((fetchOptions.headers as Record<string, string>) ?? {}),
    };

    const url = `${this.baseUrl}${path}`;
    logger.debug(`API request: ${fetchOptions.method ?? "GET"} ${url}`);

    const res = await fetch(url, { ...fetchOptions, headers });

    if (!res.ok) {
      const body = await res.text().catch(() => "");
      throw new Error(`API ${res.status}: ${res.statusText} - ${body}`);
    }

    if (res.status === 204) return undefined as T;
    return res.json() as Promise<T>;
  }

  async getCurrentGlucose(token: string): Promise<GlucoseReading[]> {
    return this.request<GlucoseReading[]>("/api/v1/entries/current.json", {
      token,
    });
  }

  async acknowledgeAllAlerts(
    token: string,
    acknowledgedBy?: string,
  ): Promise<void> {
    return this.request<void>("/api/v4/alerts/acknowledge", {
      method: "POST",
      token,
      body: JSON.stringify({ acknowledgedBy }),
    });
  }

  async muteAlerts(
    token: string,
    tenantId: string,
    minutes: number,
  ): Promise<void> {
    logger.warn("muteAlerts not yet implemented on backend");
  }

  async markDelivered(
    deliveryId: string,
    platformMessageId?: string,
    platformThreadId?: string,
  ): Promise<void> {
    return this.request<void>(
      `/api/v4/alerts/deliveries/${deliveryId}/delivered`,
      {
        method: "POST",
        token: this.serviceToken,
        body: JSON.stringify({ platformMessageId, platformThreadId }),
      },
    );
  }

  async markFailed(deliveryId: string, error: string): Promise<void> {
    return this.request<void>(
      `/api/v4/alerts/deliveries/${deliveryId}/failed`,
      {
        method: "POST",
        token: this.serviceToken,
        body: JSON.stringify({ error }),
      },
    );
  }

  async getPendingDeliveries(channelTypes: string[]): Promise<any[]> {
    const params = channelTypes
      .map((t) => `channelType=${encodeURIComponent(t)}`)
      .join("&");
    return this.request<any[]>(`/api/v4/alerts/deliveries/pending?${params}`, {
      token: this.serviceToken,
    });
  }

  async sendHeartbeat(platforms: string[]): Promise<void> {
    return this.request<void>("/api/v4/system/heartbeat", {
      method: "POST",
      token: this.serviceToken,
      body: JSON.stringify({ platforms, service: "nocturne-bot" }),
    });
  }
}
