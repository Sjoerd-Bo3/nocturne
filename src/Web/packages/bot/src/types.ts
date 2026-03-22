/** Minimal interface for the API methods the bot needs. */
export interface BotApiClient {
  sensorGlucose: {
    getAll(page?: number, pageSize?: number): Promise<{ items?: SensorGlucoseReading[] }>;
  };
  alerts: {
    acknowledgeAlerts(body: { acknowledgedBy?: string }): Promise<void>;
  };
  chatIdentity: {
    resolve(platform?: string, platformUserId?: string): Promise<ChatIdentityLink | null>;
  };
}

export interface SensorGlucoseReading {
  sgv?: number;
  direction?: string;
  mills?: number;
  dateString?: string;
  delta?: number;
}

export interface ChatIdentityLink {
  id?: string;
  tenantId?: string;
  nocturneUserId?: string;
  platform?: string;
  platformUserId?: string;
  displayUnit?: string;
  isActive?: boolean;
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

export interface AlertDispatchEvent {
  deliveryId: string;
  channelType: string;
  destination: string;
  payload: AlertPayload;
}
