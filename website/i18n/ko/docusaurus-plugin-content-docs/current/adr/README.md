---
title: 아키텍처 결정 기록
description: cMind 뒤의 명확하지 않은 설계 결정 — 컨텍스트, 결정, 그리고 결과 — 당신은 코드에서 읽을 수 없습니다.
---

# 아키텍처 결정 기록

이들은 당신이 **코드에서 추론할 수 없는** 설계 결정을 기록합니다 — 트레이드오프, 취하지 않은 경로, 그리고 이유. 각각은 짧습니다: *컨텍스트 → 결정 → 결과*. 새로운 구조적 결정 → 여기에 ADR을 추가하세요(다음 번호) 그래서 다음 엔지니어(인간 또는 AI)가 결과만이 아니라 추론을 상속합니다.

| # | 결정 |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | 순수 `Core`를 사용한 엄격한 DDD |
| [0002](./0002-tph-instance-replaces-entity.md) | 인스턴스 상태는 TPH; 전환이 엔티티를 교체합니다 |
| [0003](./0003-external-nodes-http-jwt.md) | cTrader CLI 노드는 HTTP + JWT, SSH/shell 없음 |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder`는 웹 호스트에서 샌드박스 컨테이너에서 실행됩니다 |
| [0005](./0005-anthropic-raw-http.md) | AI 클라이언트는 raw HTTP를 사용합니다, Anthropic SDK가 아닙니다 |
| [0006](./0006-copy-profile-db-lease.md) | 복사 호스팅은 원자적 DB 리스에 의해 조정됩니다 |
