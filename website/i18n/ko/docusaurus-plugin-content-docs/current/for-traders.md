---
slug: /for-traders
title: cTrader 트레이더를 위한 cMind
description: cTrader 트레이더가 cMind를 자체 호스팅해야 하는 이유 — 당신의 스택과 데이터를 소유하고, AI 기반 콘솔 하나에서 cBot을 작성, 백테스트, 실행 및 모니터링하세요, 당신의 노트북, VPS 또는 휴대폰에서.
keywords:
  - cTrader
  - 알고리즘 거래
  - 자체 호스팅 거래 플랫폼
  - cBot 백테스팅
  - AI 거래 봇
  - 오픈 소스 거래 소프트웨어
sidebar_position: 5
---

# cTrader 트레이더를 위한 cMind 📈

당신은 이미 cTrader에서 거래 중입니다. 당신은 이미 코드 편집기, 백테스터, VPS, 그리고 3개의 브라우저 탭을 저글링합니다. **cMind는 모든 것을 당신이 직접 실행하는 하나의 어두운, 키보드 친화적 콘솔로 축소합니다** — 그리고 오픈 소스이므로 당신의 우위, 전략, 또는 자격 증명에 대한 아무것도 절대 당신의 박스를 떠나지 않습니다.

:::tip TL;DR
노트북, 저렴한 VPS, 또는 홈 서버에서 cMind를 자체 호스팅하세요. AI 코어가 잡무를 하면서 한 곳에서 cBot을 작성, 백테스트, 실행 및 모니터링하세요. → [5분 안에 실행](./deployment/local.md)
:::

## 호스팅 서비스 대신 자체 호스팅하는 이유?

- **당신의 스택과 데이터를 소유하세요.** 당신의 cBot, 자격 증명, 토큰, 자산 이력은 **당신의** 인프라에 있습니다 — 제3자 없음, 록인 없음, "우리가 이 제품을 서비스 종료합니다" 이메일 없음.
- **정말로 변경할 수 있도록 당신의 것입니다.** C# 14 / .NET 10, 엄격한 DDD, EF Core + PostgreSQL, MCP 서버 — 모두 오픈 소스이고 해킹 가능합니다. 포크, 확장, PR 전송.
- **기능별 페이월 없음.** 모든 제공자에 대한 당신 자신의 AI 키를 가져오세요; 모든 AI 기능이 켜져 있습니다.

직접 서버를 실행하지 않기를 선호하시나요? 호스팅 회사가 관리형 cMind를 당신을 위해 실행할 수 있습니다 — [클라우드 & VPS 제공자를 위해](./for-cloud-providers.md)를 참조하세요.

## 하나의 콘솔, 탭 저글링 없음

- **실제 Monaco IDE(VS Code 편집기)에서 작성**, C# **및** Python 템플릿 및 일회용 컨테이너의 샌드박싱된 `dotnet build`. → [빌드 & 백테스트](./features/build-and-backtest.md)
- **노드 플릿을 통해 백테스트**하고 자산 곡선이 실시간으로 스트림백을 봅니다.
- **전략을 실시간으로 실행**하고 **하나의 대시보드에서 모니터링**하세요. → [대시보드](./features/dashboard.md)
- **마스터 계정을 많은 계정 전체에 복사**하세요 브로커와 cTrader ID 전체에서, dropped 연결 및 rotating 토큰을 견딜 수 있는 조정을 사용합니다. → [복사 거래](./features/copy-trading.md)

## 잡담이 아닌 잡무를 하는 AI

당신의 API 키를 가져오세요(지원되는 모든 제공자 — 클라우드 또는 로컬 모델) 그리고 평문 영어 → 자체 수리 루프, 파라미터 조정, 백테스트 사후 검토, misbehaving 봇을 자동 중지할 수 있는 위험 가드가 있는 실제로 컴파일되는 cBot을 가져오세요. → [AI 코어 만나기](./features/ai.md)

## 기관급 도구, 한 사람을 위해

동일한 엄격함 한 데스크가 비용을 지불합니다, 당신의 박스:

- [백테스트 무결성](./features/backtest-integrity.md) · [포지션 사이징](./features/position-sizing.md)
- [전략 건강](./features/strategy-health.md) · [레짐 랩](./features/regime-lab.md)
- [실행 TCA](./features/execution-tca.md) · [거래 저널](./features/trading-journal.md)
- [에이전트 스튜디오](./features/agent-studio.md) · [역 포지셔닝](./features/contrarian-positioning.md)

## 당신이 하는 곳에서 실행됩니다

`docker compose up`으로 노트북에서 시작하고, 준비가 되면 저렴한 VPS 또는 홈 서버로 졸업하고, 휴대폰에서 당신의 봇을 확인합니다 — cMind는 설치 가능한, 모바일 우선 [PWA](./features/pwa.md). → [로컬에서 실행](./deployment/local.md)

당신의 AI 클라이언트가 그것을 구동하고 싶으신가요? 내장된 [MCP 서버](./features/mcp.md)가 있습니다.

## 더 나아지도록 도우세요

cMind는 오픈 소스이고 MIT 라이센스됨 — 로드맵은 커뮤니티 형성:

- 이슈 및 기능 요청 파일, 그리고 중요한 것에 투표.
- cBot 템플릿, AI 제공자 어댑터, 또는 UI 번역 추가.
- PR 전송 — 3개의 테스트 계층(unit + integration + E2E) 및 엄격한 DDD가 표준을 높게 유지하고, [기여 가이드](./contributing.md)가 당신을 안내합니다.

준비되셨나요? → [소개 읽기](./intro.md) 그 다음 [로컬에서 실행](./deployment/local.md).
