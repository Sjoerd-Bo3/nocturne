import { Card, Fields, Field } from "chat";
import type { GlucoseReading } from "../lib/nocturne-client.js";

const TREND_ARROWS: Record<string, string> = {
  DoubleUp: "^^",
  SingleUp: "^",
  FortyFiveUp: "/",
  Flat: "->",
  FortyFiveDown: "\\",
  SingleDown: "v",
  DoubleDown: "vv",
  "NOT COMPUTABLE": "?",
  "RATE OUT OF RANGE": "?",
};

function formatGlucose(mgdl: number, unit: "mg/dL" | "mmol/L"): string {
  if (unit === "mmol/L") return `${(mgdl / 18.0182).toFixed(1)} mmol/L`;
  return `${mgdl} mg/dL`;
}

function timeAgo(dateMs: number): string {
  const diffMs = Date.now() - dateMs;
  const minutes = Math.round(diffMs / 60000);
  if (minutes < 1) return "just now";
  if (minutes === 1) return "1 min ago";
  return `${minutes} min ago`;
}

export function GlucoseCard(props: {
  reading: GlucoseReading;
  unit?: "mg/dL" | "mmol/L";
}) {
  const { reading, unit = "mg/dL" } = props;
  const value = formatGlucose(reading.sgv, unit);
  const arrow = TREND_ARROWS[reading.direction] ?? reading.direction;
  const delta =
    reading.delta != null
      ? `${reading.delta > 0 ? "+" : ""}${formatGlucose(reading.delta, unit)}`
      : "N/A";

  return (
    <Card title="Glucose Reading">
      <Fields>
        <Field label="BG" value={`${value} ${arrow}`} />
        <Field label="Delta" value={delta} />
        <Field label="Updated" value={timeAgo(reading.date)} />
      </Fields>
    </Card>
  );
}
