import { Card, CardText, Fields, Field, Actions, Button } from "chat";
import type { AlertPayload } from "../lib/nocturne-client.js";

const TREND_ARROWS: Record<string, string> = {
  DoubleUp: "^^",
  SingleUp: "^",
  FortyFiveUp: "/",
  Flat: "->",
  FortyFiveDown: "\\",
  SingleDown: "v",
  DoubleDown: "vv",
};

function formatGlucose(mgdl: number, unit: "mg/dL" | "mmol/L"): string {
  if (unit === "mmol/L") return `${(mgdl / 18.0182).toFixed(1)} mmol/L`;
  return `${mgdl} mg/dL`;
}

export function AlertCard(props: {
  payload: AlertPayload;
  unit?: "mg/dL" | "mmol/L";
}) {
  const { payload, unit = "mg/dL" } = props;
  const value =
    payload.glucoseValue != null
      ? formatGlucose(payload.glucoseValue, unit)
      : "N/A";
  const arrow = payload.trend
    ? (TREND_ARROWS[payload.trend] ?? payload.trend)
    : "";

  return (
    <Card title={`Alert: ${payload.ruleName}`}>
      <CardText>{`${payload.subjectName} is ${value} ${arrow}`}</CardText>
      <Fields>
        <Field
          label="Time"
          value={new Date(payload.readingTimestamp).toLocaleTimeString()}
        />
        {payload.trendRate != null && (
          <Field
            label="Rate"
            value={`${payload.trendRate > 0 ? "+" : ""}${payload.trendRate.toFixed(1)}/min`}
          />
        )}
      </Fields>
      <Actions>
        <Button id="ack_alert" value={payload.tenantId} style="primary">
          Acknowledge
        </Button>
        <Button id="mute_30" value={payload.tenantId}>
          Mute 30 min
        </Button>
      </Actions>
    </Card>
  );
}

export function AcknowledgedCard(props: {
  originalTitle: string;
  acknowledgedBy: string;
}) {
  return (
    <Card title={`${props.originalTitle} [Acknowledged]`}>
      <CardText>{`Acknowledged by ${props.acknowledgedBy}`}</CardText>
    </Card>
  );
}

export function ResolvedCard(props: {
  originalTitle: string;
  resolvedValue?: number;
  unit?: "mg/dL" | "mmol/L";
}) {
  const { unit = "mg/dL" } = props;
  const valueStr =
    props.resolvedValue != null
      ? formatGlucose(props.resolvedValue, unit)
      : "";
  return (
    <Card title={`${props.originalTitle} [Resolved]`}>
      <CardText>{`Resolved${valueStr ? ` -- back in range (${valueStr})` : ""}`}</CardText>
    </Card>
  );
}
