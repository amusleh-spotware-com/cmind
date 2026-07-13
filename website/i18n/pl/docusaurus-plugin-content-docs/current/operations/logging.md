---
description: "Wszystkie trzy usługi (Web, MCP, CtraderCliNode) logują przez Serilog jako kompaktowy JSON na stdout — container runtime'y i kolektory logów (Loki, ELK, CloudWatch…"
---

# Logowanie i obserwowanie

Wszystkie trzy usługi (Web, MCP, CtraderCliNode) logują przez **Serilog** jako **kompaktowy JSON na stdout** — container runtime'y i kolektory logów (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) zużywają strukturalne zdarzenia bezpośrednio, brak free-text parsing.

## Rurociąg

- **Sink 1 — Konsola (kompaktowy JSON).** `RenderedCompactJsonFormatter`; każde zdarzenie nosi pełną tożsamość zasobu OpenTelemetry — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — plus `trace_id` / `span_id` z otoczenia `Activity` (`ActivityEnricher`) i `LogContext` zakresy. Trace id pozwala CloudWatch Logs Insights i Azure Log Analytics obracać log↔trace **nawet bez wdrożonego kolektora**.
- **Sink 2 — OTLP (opcjonalnie).** Gdy `OTEL_EXPORTER_OTLP_ENDPOINT` ustawiony, dzienniki również eksportują przez OTLP z tymi samymi atrybutami zasobu, obok OpenTelemetry **metryki** i **ślady** (ASP.NET Core, HttpClient, instrumentacja runtime) z `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (opcjonalnie).** Gdy `APPLICATIONINSIGHTS_CONNECTION_STRING` ustawiony, ślady i metryki eksportują **natywnie** do Application Insights (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — bez kolektora. Patrz sekcja native poniżej.
- **Wiadomości generowane źródłem.** Dzienniki aplikacji używają silnie typizowanego `LogMessages` rozszerzenia (`Core/Logging/LogMessages.cs`) ze stabilnymi `EventId`s — nigdy ad-hoc `ILogger.LogInformation`.
- **Logowanie żądań.** `UseSerilogRequestLogging()` emituje jedno strukturalne podsumowanie na żądanie HTTP (metoda, ścieżka, status, upływ ms).

## Konfiguracja

Poziomy możliwe na usługę poprzez sekcję `Serilog` w `appsettings.json` (przeczytaj przez `ReadFrom.Configuration`), np.:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore": "Warning" }
    }
  }
}
```

Przesłoń w runtime ze zmiennymi env, np. `Serilog__MinimumLevel__Default=Debug`.

## Wysyłka do kolektora

Ustaw jedną zmienną env na usługę, wskaż na endpoint OTLP:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / chmura: dodaj zmienną env do każdej usługi.

Z kolektora, wentyluj do backendu (Tempo/Jaeger ślady, Prometheus metryki, Loki dzienniki) ze śladem↔dziennik korelacji nienaruszony.

## Natywne backendy chmury (bez dodatkowego kolektora)

Oba wdrażania zarządzane przewodowała dla natywnego stosu obserwowania z pudełka — nie potrzeba kolektora OTLP.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` dostarcza **komponent Application Insights oparty na workspace**, przechodzi jego ciąg połączenia do Web i MCP jako `APPLICATIONINSIGHTS_CONNECTION_STRING`. Wynik:

- **Ślady + metryki** przepływają natywnie do App Insights (Application Map, na żywo metryki, wyszukiwanie transakcji od końca do końca), skorelowane przez `trace_id`.
- **Dzienniki** (kompaktowy JSON na stdout) lądują w **tej samej Log Analytics workspace** poprzez Container Apps `appLogsConfiguration`, więc `AppTraces` / `ContainerAppConsoleLogs_CL` dołączają do śledzenia ID.
- Ustaw opcjonalny parametr `otlpEndpoint` Bicep do *także* wentyluj do kolektora.

Zapytaj dzienniki w Log Analytics (linia JSON w `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
