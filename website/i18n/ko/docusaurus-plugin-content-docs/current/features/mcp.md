---
description: "cMind는 Model Context Protocol (MCP) 서버를 별도 프로세스/배포로 배포합니다 — Web 앱과 독립적으로 확장 + 재배포. cBot, 인스턴스, AI 도구를 노출합니다..."
---

# MCP 서버

cMind는 Model Context Protocol (MCP) 서버를 **별도 프로세스/배포**로 배포합니다 — Web 앱과 독립적으로 확장 + 재배포. HTTP + SSE 전송을 통해 MCP 클라이언트 (예: AI 어시스턴트)에 cBot, 인스턴스, AI 도구를 노출합니다.

## 인증

- 사용자당 API 키 `mcpk_<hex>`, SHA-256 해시, 접두사 인덱스 (`McpKeyAuthHandler`). **Mcp** 페이지에서 관리 (`McpApiKey` 집계).
- `AddHttpContextAccessor`로 상태 비 저장 HTTP 전송 — 도구 호출은 인증된 사용자로 실행됩니다.

## 도구

- `CBotTools` — cBots 작성 / 빌드.
- `InstanceTools` — 인스턴스 실행 / 백테스트 / 검사.
- `AiTools` — 생성, 검토, 감정, 백테스트 분석, 복사 도구.

## 옵스

`/version` 노출; 모든 환경에서 매핑된 상태 엔드포인트 (`/health`, `/alive`) K8s/클라우드 프로브용. 구조화된 Serilog JSON + OpenTelemetry, Web 앱과 동일합니다.
