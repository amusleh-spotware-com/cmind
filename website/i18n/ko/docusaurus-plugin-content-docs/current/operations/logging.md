---
description: "세 가지 서비스 (Web, MCP, CtraderCliNode) 모두 Serilog를 통해 stdout에서 압축 JSON으로 로그합니다 — 컨테이너 런타임 및 로그 컬렉터 (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog)가 구조화된 이벤트를 직접 수집, 자유 텍스트 파싱 없음."
---

# 로깅 및 관찰 가능성

세 가지 서비스 (Web, MCP, CtraderCliNode) 모두 **Serilog**를 통해 **stdout에서 압축 JSON**으로 로그합니다 — 컨테이너 런타임 및 로그 컬렉터 (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog)가 구조화된 이벤트를 직접 수집, 자유 텍스트 파싱 없음.

## 파이프라인

- **싱크 1 — 콘솔 (압축 JSON).** `RenderedCompactJsonFormatter`; 모든 이벤트는 완전한 OpenTelemetry 리소스 ID를携带합니다 — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — plus `trace_id` / `span_id`는 주변 `Activity` (`ActivityEnricher`) 및 `LogContext` 범위에서. 추적 ID를 사용하면 CloudWatch Logs Insights 및 Azure Log Analytics가 컬렉터가 배포되지 않았더라도 로그↔추적을 피벗할 수 있습니다 **.
- **싱크 2 — OTLP (선택 사항).** `OTEL_EXPORTER_OTLP_ENDPOINT` 설정 시 로그는 동일한 리소스 属性으로 OTLP로 내보내며 OpenTelemetry **메트릭** 및 **추적**과 함께 (`AddAppTelemetry`에서 ASP.NET Core, HttpClient, 런타임 계측).
- **싱크 3 — Azure Monitor (선택 사항).** `APPLICATIONINSIGHTS_CONNECTION_STRING` 설정 시 추적 및 메트릭이 Application Insights로 **네이티브로** 내보내집니다 (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — 컬렉터 없음. 아래 클라우드 네이티브 섹션을 참조하세요.
- **소스 생성 메시지.** 앱 로그는 안정적인 `EventId`와 함께 강력한 `LogMessages` 확장을 사용합니다 (`Core/Logging/LogMessages.cs`) — 절대 임시 `ILogger.LogInformation` 안 함.
- **요청 로깅.** `UseSerilogRequestLogging()`은 HTTP 요청당 하나의 구조화된 요약을 방출합니다 (메서드, 경로, 상태, 경과 ms).

## 구성

`appsettings.json`의 `Serilog` 섹션을 통해 서비스당 수준 조정 가능 (구성을 통해 읽기):

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

런타임에서 env 변수로 재정의, 예: `Serilog__MinimumLevel__Default=Debug`.

## 컬렉터로 ship

서비스당 하나의 env var 설정, OTLP 엔드포인트를 가리킵니다:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
-.Compose / 클라우드: 각 서비스에 env var 추가.

컬렉터에서 백엔드로 fan out (Tempo/Jaeger 추적, Prometheus 메트릭, Loki 로그) trace↔log 상관관계가 온전히 유지됩니다.

## 클라우드 네이티브 백엔드 (추가 컬렉터 없음)

두 관리형 배포 모두 즉시 box에서 네이티브 관찰 가능성 스택에 연결됩니다.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep`는 **workspace 기반 Application Insights** 구성 요소를 프로비저닝하고 해당 연결 문자열을 Web 및 MCP에 `APPLICATIONINSIGHTS_CONNECTION_STRING`로 전달합니다. 결과:

- **추적 + 메트릭**이 App Insights로 네이티브로 흐릅니다 (Application Map, 라이브 메트릭, 종단 간 트랜잭션 검색), `trace_id`로 상관관계.
- **로그** (stdout의 압축 JSON)는 Container Apps `appLogsConfiguration`을 통해 **동일한 Log Analytics 작업 영역**에 착륙하므로 `AppTraces` / `ContainerAppConsoleLogs_CL`가 trace id에서 조인됩니다.
- 선택적 `otlpEndpoint` Bicep 매개변수를 설정하여 컬렉터에도 fan out합니다.

Log Analytics에서 로그 쿼리 (JSON 줄의 `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT 사이드카)

`deploy/aws/main.tf`는 각 Fargate 작업에서 **AWS Distro for OpenTelemetry (ADOT) 컬렉터**를 사이드카로 실행합니다. 앱은 `http://localhost:4317`로 OTLP를 내보냅니다; 사이드카는 ship합니다:

- **추적 → AWS X-Ray** (`awsxray` 내보내기),
- **메트릭 → CloudWatch** (`awsemf`, 네임스페이스 `cmind`, 로그 그룹 `/ecs/<prefix>/metrics`).
- **로그**는 `awslogs` 드라이버에서 압축 JSON으로 유지됩니다; CloudWatch Logs Insights가 JSON 필드를 자동 검색하므로 `trace_id`, `service.name`, `@l` 등에서 `filter` / `stats`할 수 있습니다.

작업 역할 (`aws_iam_role.task`)에는 `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`가 있습니다.

CloudWatch Logs Insights에서 로그 쿼리:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## 건강 엔드포인트 (프로브에서도 사용됨)

| 엔드포인트 | 서비스 | 의미 |
|----------|---------|---------|
| `/alive` | Web | 라이브니스 — 프로세스만. |
| `/health` | Web | 준비 상태 — 데이터베이스 확인 포함. |
| `/version` | Web, MCP | 제품 및 프로토콜 버전 (MCP 라이브니스/준비). |

**모든** 환경에서 매핑됩니다 (이전 Dev 전용) Kubernetes 및 클라우드 프로브가 프로덕션에서 작동합니다.
