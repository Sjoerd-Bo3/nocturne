import { Card, Fields, Field } from "chat";
import type { SensorGlucoseReading } from "../types.js";
import { formatGlucose, trendArrow } from "../lib/format.js";

function timeAgo(dateMs: number): string {
  const diffMs = Date.now() - dateMs;
  const minutes = Math.round(diffMs / 60000);
  if (minutes < 1) return "just now";
  if (minutes === 1) return "1 min ago";
  return `${minutes} min ago`;
}

export function GlucoseCard(props: {
  reading: SensorGlucoseReading;
  unit?: "mg/dL" | "mmol/L";
}) {
  const { reading, unit = "mg/dL" } = props;
  const value = reading.sgv != null ? formatGlucose(reading.sgv, unit) : "N/A";
  const arrow = reading.direction ? trendArrow(reading.direction) : "";
  const delta =
    reading.delta != null
      ? `${reading.delta > 0 ? "+" : ""}${formatGlucose(reading.delta, unit)}`
      : "N/A";

  return (
    <Card title="Glucose Reading">
      <Fields>
        <Field label="BG" value={`${value} ${arrow}`} />
        <Field label="Delta" value={delta} />
        <Field label="Updated" value={reading.mills != null ? timeAgo(reading.mills) : "N/A"} />
      </Fields>
    </Card>
  );
}
