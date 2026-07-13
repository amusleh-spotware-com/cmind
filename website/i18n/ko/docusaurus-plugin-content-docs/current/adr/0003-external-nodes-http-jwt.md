---
title: 0003 — cTrader CLI 노드는 HTTP + JWT, SSH/shell 없음
description: 원격 노드 에이전트가 왜 단기 JWT를 가진 HTTP API만 노출하고 절대 셸을 노출하지 않는지.
---

# 0003 — cTrader CLI 노드는 HTTP + JWT, SSH/shell 없음

## 컨텍스트

백테스트/실행 컨테이너는 원격 호스트에서 실행됩니다. 명백한 접근 — SSH 및 docker 실행 — 메인 앱에 임의의 원격 코드 실행 및 모든 노드에서 장시간 자격 증명을 제공합니다. 그것은 신뢰할 수 없는 사용자 cBot을 실행하는 시스템에 대한 큰 블래스트 반경입니다.

## 결정

각 원격 호스트는 **SSH 없고 셸 없는** 독립형 `CtraderCliNode` **HTTP 에이전트**를 실행합니다. 메인 앱은 HTTP를 통해 에이전트를 호출합니다; 모든 요청은 해당 노드의 비밀로 서명된 단기 **HS256 JWT**(5분, `iss=app-main` / `aud=app-node`)를 전달합니다. 에이전트:

- `AllowedImagePrefix`와 일치하는 이미지만 실행합니다(경로 경계가 있어서 `ghcr.io/spotware`는 `ghcr.io/spotware-evil/...`을 일치시킬 수 없습니다);
- `ArgumentList`를 통해 docker을 실행합니다 — 절대 셸 문자열이 아닙니다;
- **stateless**이고, `app.instance` 레이블로 컨테이너를 찾습니다;
- 자체 등록되고 `POST /api/nodes/register`로 하트비트합니다; 메인 앱은 `CtraderCliNode`를 **이름으로** upserts합니다, 노드가 IP 변경 시에도 유지됩니다.

## 결과

- leaked 요청 토큰은 몇 분 내에 만료됩니다; 훔칠 서 있는 셸 자격 증명이 없습니다.
- 에이전트의 기능은 "허용된 이미지 실행"으로 제한됩니다 — 일반 원격 셸로 전환될 수 없습니다.
- 노드 정체성은 이름 기반이므로, 새 IP로 노드를 re-provisioning하는 것은 히스토리를 고아로 만들지 않습니다.
