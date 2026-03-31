# Compatibility Proxy Configuration

The compatibility proxy runs background comparisons between Nocturne and your upstream Nightscout instance. It intercepts GET requests on v1/v2/v3 API paths, lets Nocturne respond normally, then asynchronously forwards the same request to Nightscout and compares the two responses. No latency is added to client responses.

Results feed the Nightscout transition dashboard, giving you confidence that Nocturne produces identical API responses before you disconnect your Nightscout instance.

## How It Works

1. A GET request arrives on a legacy API path (e.g. `/api/v1/entries.json`).
2. Nocturne handles the request and responds to the client immediately.
3. In the background, the proxy clones the request and forwards it to Nightscout.
4. The two responses are compared and any discrepancies are persisted.
5. The transition dashboard aggregates these results into a compatibility score.

POST/PUT/DELETE requests are never forwarded; only read operations are compared.

## Prerequisites

The proxy reads the Nightscout URL and API secret from the **Nightscout connector configuration**, not from the proxy configuration itself. Ensure the Nightscout connector is configured and enabled before activating the proxy.

## Configuration

All proxy settings live under `Parameters:CompatibilityProxy` in `appsettings.json`:

```json
{
  "Parameters": {
    "CompatibilityProxy": {
      "Enabled": true,
      "TimeoutSeconds": 30,
      "RetryAttempts": 3,
      "EnableDetailedLogging": false,
      "EnableCorrelationTracking": true,

      "Comparison": {
        "ExcludeFields": ["timestamp", "date", "dateString", "_id", "id", "sysTime", "mills", "created_at", "updated_at"],
        "AllowSupersetResponses": true,
        "TimestampToleranceMs": 5000,
        "NumericPrecisionTolerance": 0.001,
        "NormalizeFieldOrdering": true,
        "ArrayOrderHandling": "Strict",
        "EnableDeepComparison": true
      },

      "CircuitBreaker": {
        "FailureThreshold": 5,
        "RecoveryTimeoutSeconds": 60,
        "SuccessThreshold": 3
      },

      "Redaction": {
        "Enabled": true,
        "SensitiveFields": [],
        "ReplacementText": "[REDACTED]",
        "RedactUrlParameters": true,
        "UrlParametersToRedact": ["token", "api_secret", "secret", "key"]
      },

      "DiscrepancyForwarding": {
        "Enabled": false,
        "SaveRawData": false,
        "DataDirectory": "discrepancies",
        "EndpointUrl": "",
        "ApiKey": "",
        "SourceId": "",
        "MinimumSeverity": "Minor",
        "TimeoutSeconds": 10,
        "RetryAttempts": 3,
        "RetryDelayMs": 1000
      }
    }
  }
}
```

## Core Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `false` | Activate the background comparison proxy |
| `TimeoutSeconds` | int | `30` | Timeout for forwarded requests to Nightscout |
| `RetryAttempts` | int | `3` | Retry attempts for failed Nightscout requests |
| `EnableDetailedLogging` | bool | `false` | Log full request/response details |
| `EnableCorrelationTracking` | bool | `true` | Track correlation IDs across comparisons |

## Comparison Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExcludeFields` | string[] | See above | Fields excluded from comparison (timestamps, IDs) |
| `AllowSupersetResponses` | bool | `true` | Allow Nocturne to return extra fields |
| `TimestampToleranceMs` | long | `5000` | Tolerance for timestamp differences |
| `NumericPrecisionTolerance` | double | `0.001` | Tolerance for floating-point differences |
| `NormalizeFieldOrdering` | bool | `true` | Ignore JSON property order |
| `ArrayOrderHandling` | enum | `Strict` | `Strict`, `Loose`, or `Sorted` |
| `EnableDeepComparison` | bool | `true` | Deep-compare nested objects |

## Circuit Breaker Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FailureThreshold` | int | `5` | Consecutive failures before opening circuit |
| `RecoveryTimeoutSeconds` | int | `60` | Wait time before attempting recovery |
| `SuccessThreshold` | int | `3` | Successes needed to close circuit |

## Redaction Settings

Sensitive data is always redacted from error messages and logs. Mandatory fields (`api_secret`, `token`, `password`, `key`, `secret`, `authorization`) are always redacted regardless of configuration.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable additional redaction |
| `SensitiveFields` | string[] | `[]` | Extra fields to redact beyond mandatory ones |
| `ReplacementText` | string | `[REDACTED]` | Replacement text for redacted values |
| `RedactUrlParameters` | bool | `true` | Redact sensitive URL query parameters |

## Discrepancy Forwarding

Optionally forward discrepancy reports to a remote monitoring endpoint.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable forwarding to remote endpoint |
| `SaveRawData` | bool | `false` | Save discrepancy JSON files locally |
| `DataDirectory` | string | `discrepancies` | Local directory for raw data files |
| `EndpointUrl` | string | `""` | Remote monitoring endpoint URL |
| `ApiKey` | string | `""` | Bearer token for authentication |
| `SourceId` | string | `""` | Instance identifier |
| `MinimumSeverity` | enum | `Minor` | Minimum severity to forward: `Minor`, `Major`, `Critical` |

## Transition Dashboard Integration

When the proxy is enabled, the Nightscout transition dashboard at `/settings/nightscout-transition` displays:

- **Compatibility score** as a percentage (based on all background comparisons)
- **Total comparisons** performed
- **Discrepancy count** (major + critical differences)
- A **disconnect blocker** if the compatibility score falls below 95%

The compatibility score directly informs the disconnect readiness recommendation. A score below 95% prevents the dashboard from showing "Safe to Disconnect", prompting you to investigate the discrepancies via the compatibility API at `/api/v4/compatibility/`.
