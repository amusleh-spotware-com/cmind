---
description: "Les trois services (Web, MCP, CtraderCliNode) enregistrent via Serilog en tant que JSON compact sur stdout — les runtimes de conteneur et les collecteurs de journaux (Loki, ELK, CloudWatch…"
---

# Journalisation & observabilité

Les trois services (Web, MCP, CtraderCliNode) enregistrent via **Serilog** en tant que **JSON compact sur stdout** — les runtimes de conteneur et les collecteurs de journaux (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) ingèrent directement les événements structurés, pas d'analyse de texte libre.

## Pipeline

- **Sink 1 — Console (JSON compact).** `RenderedCompactJsonFormatter`; chaque événement porte l'identité complète de la ressource OpenTelemetry — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — plus `trace_id` / `span_id` depuis `Activity` ambiant (`ActivityEnricher`) et les scopes `LogContext`. Les ID de trace permettent à CloudWatch Logs Insights et Azure Log Analytics de faire pivoter le journal ↔ trace **même sans collecteur déployé**.
- **Sink 2 — OTLP (optionnel).** Quand `OTEL_EXPORTER_OTLP_ENDPOINT` est défini, les logs exportent également sur OTLP avec les mêmes attributs de ressource, aux côtés des **métriques** OpenTelemetry et **traces** (ASP.NET Core, HttpClient, instrumentation runtime) depuis `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (optionnel).** Quand `APPLICATIONINSIGHTS_CONNECTION_STRING` est défini, les traces et les métriques exportent **nativement** vers Application Insights (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — pas de collecteur. Voir la section cloud-native ci-dessous.
- **Messages générés par source.** Les journaux d'application utilisent des extensions `LogMessages` fortement typées (`Core/Logging/LogMessages.cs`) avec `EventId`s stables — jamais ad-hoc `ILogger.LogInformation`.
- **Journalisation des requêtes.** `UseSerilogRequestLogging()` émet un résumé structuré par requête HTTP (méthode, chemin, statut, ms écoulé).

## Configuration

Niveaux ajustables par service via la section `Serilog` dans `appsettings.json` (lue via `ReadFrom.Configuration`), par exemple :

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

Remplacez à l'exécution avec les variables env, par exemple `Serilog__MinimumLevel__Default=Debug`.

## Expédition vers un collecteur

Définir une variable env par service, pointer vers le point de terminaison OTLP :

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm : `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud : ajouter la variable env à chaque service.

Depuis le collecteur, fan out vers le backend (traces Tempo/Jaeger, métriques Prometheus, logs Loki) avec la corrélation trace ↔ log intacte.

## Backends cloud-native (pas de collecteur supplémentaire)

Les deux déploiements gérés sont câblés pour la pile d'observabilité native prête à l'emploi — aucun collecteur OTLP nécessaire.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` provisionne le composant **Application Insights basé sur workspace**, passe sa chaîne de connexion à Web et MCP en tant que `APPLICATIONINSIGHTS_CONNECTION_STRING`. Résultat :

- Les **traces + métriques** s'écoulent nativement dans App Insights (Application Map, métriques en direct, recherche de transaction de bout en bout), corrélées par `trace_id`.
- Les **logs** (JSON compact sur stdout) arrivent dans le **même workspace Log Analytics** via Container Apps `appLogsConfiguration`, afin que `AppTraces` / `ContainerAppConsoleLogs_CL` se joignent sur l'ID de trace.
- Définir le paramètre Bicep optionnel `otlpEndpoint` pour *aussi* fan out vers un collecteur.

Interroger les logs dans Log Analytics (ligne JSON dans `Log_s`) :

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (sidecar ADOT)

`deploy/aws/main.tf` exécute le **collecteur AWS Distro for OpenTelemetry (ADOT) en tant que sidecar** dans chaque tâche Fargate. L'application exporte OTLP vers `http://localhost:4317`; le sidecar expédie :

- **traces → AWS X-Ray** (exportateur `awsxray`),
- **métriques → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Les logs** restent sur le pilote `awslogs` en tant que JSON compact ; CloudWatch Logs Insights découvre automatiquement les champs JSON, afin que vous `filter` / `stats` sur `trace_id`, `service.name`, `@l`, etc.

Le rôle de tâche (`aws_iam_role.task`) porte `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Interroger les logs dans CloudWatch Logs Insights :

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Points de terminaison de santé (aussi utilisés par les sondes)

| Point de terminaison | Service | Signification |
|----------|---------|---------|
| `/alive` | Web | Liveness — processus uniquement. |
| `/health` | Web | Readiness — inclut la vérification de la base de données. |
| `/version` | Web, MCP | Version produit + protocole (liveness/readiness MCP). |

Mappée dans **tous** les environnements (précédemment Dev-only) afin que les sondes Kubernetes et cloud fonctionnent en production.
