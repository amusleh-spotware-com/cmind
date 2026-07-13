---
description: "Logging — structured JSON logs, korelasi trace, dan integrasi observability."
---

# Logging

Logging — structured JSON logs, korelasi trace, dan integrasi observability.

## Format Log

Semua log dalam format JSON struktural:

```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "Information",
  "message": "Order placed successfully",
  "trace_id": "abc123def456",
  "span_id": "xyz789",
  "user_id": "usr-123",
  "account_id": "acc-456",
  "order_id": "ord-789",
  "symbol": "EURUSD",
  "properties": {
    "order_type": "market",
    "lots": 0.5,
    "price": 1.0850
  }
}
```

## Log Levels

| Level | Penggunaan |
|-------|------------|
| **Trace** | Detail sangat granular (development only) |
| **Debug** | Informasi debugging |
| **Information** | Events normal (request received, order filled) |
| **Warning** | Event tidak biasa tapi tidak error |
| **Error** | Error yang butuh perhatian |
| **Critical** | Kegagalan sistem |

## Source-Generated Logging

Menggunakan source-generated log messages (tidak `ILogger.Log` langsung):

```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} placed for {Symbol}")]
public static partial void LogOrderPlaced(this ILogger logger, string orderId, string symbol);

// Usage
_logger.LogOrderPlaced(orderId, symbol);
```

Ini menghasilkan log yang di-optimasi dengan compile-time checks.

## Structured Logging

### Dengan Message Templates

```csharp
_logger.LogInformation(
    "Order {OrderId} for {Symbol} filled at {Price} with {Pnl} P&L",
    orderId, symbol, fillPrice, pnl);
```

### Dengan Objects

```csharp
_logger.LogInformation(
    "Order completed: {OrderSummary}",
    new { orderId, symbol, fillPrice, pnl });
```

## Trace Correlation

### Automatic Propagation

`trace_id` dan `span_id` di-propagate otomatis:

```csharp
// Child span
using var span = _tracer.StartActiveSpan("ProcessOrder");
span.SetAttribute("order_id", orderId);

// Log otomatis termasuk span context
_logger.LogInformation("Processing order {OrderId}", orderId);
// Output: { "trace_id": "abc123", "span_id": "child123", "order_id": "ord-456" }
```

### Manual Correlation

```csharp
// Correlate manual operation
var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
using (_logger.BeginScope(new Dictionary<string, object> { ["correlation_id"] = correlationId }))
{
    _logger.LogInformation("Starting correlated operation");
}
```

## Log Sinks

### Console (Development)

```json
{
  "Logging": {
    "Console": {
      "FormatterName": "json"
    }
  }
}
```

### File (JSON, rotation harian)

```json
{
  "Logging": {
    "File": {
      "Path": "/logs/cmind-{Date}.json",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30
    }
  }
}
```

### CloudWatch (AWS)

```json
{
  "Logging": {
    "CloudWatch": {
      "LogGroup": "/aws/cmind/app",
      "StreamName": "{InstanceId}",
      "BatchSize": 100,
      "Period": "00:00:05"
    }
  }
}
```

### X-Ray / ADOT

Traces otomatis di-export via ADOT sidecar:

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

## Query Logs

### Dengan Serilog

```csharp
// Di code
Log.Information("User {UserId} placed order {OrderId}", userId, orderId);

// Di query (Elasticsearch/kibana)
message: "placed order" AND userId: "usr-123"
```

### Common Queries

```bash
# Find all errors for user
level:Error AND user_id: "usr-123"

# Find order-related logs
message: "*order*" AND trace_id: "abc123"

# Find slow requests
elapsed_ms: >5000

# Find all logs in trace
trace_id: "abc123" | sort timestamp ASC
```

## Log Retention

| Environment | Retention | Storage |
|-------------|-----------|---------|
| Development | 7 days | Local file |
| Staging | 30 days | S3 |
| Production | 90 days | CloudWatch/S3 |

## Best Practices

1. **No sensitive data** — jangan log password, token, PII.
2. **Use correct level** — Information untuk events normal, bukan Debug.
3. **Structured always** — selalu gunakan message templates, bukan string concatenation.
4. **Include context** — trace_id, user_id, account_id di setiap log.
5. **Meaningful messages** — "Order placed" bukan "Order processed".
