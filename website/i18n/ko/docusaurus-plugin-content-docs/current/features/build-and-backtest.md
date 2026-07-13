---
description: "인-browser Monaco IDE에서 cTrader cBots(C# 및 Python, 모두 .NET)를 빌드, 실행, 백테스트하며, 공식 ghcr.io/spotware/ctrader-console 이미지로 실행됩니다."
---

# cBot 빌드 및 백테스트

인-browser Monaco IDE에서 cTrader cBots(C# **및** Python, 모두 .NET)를 빌드, 실행, 백테스트하며, 공식 `ghcr.io/spotware/ctrader-console` 이미지로 실행됩니다.

## 빌드

- **Builder** 페이지가 Monaco 편집기를 호스트합니다; `CBotBuilder`가 **버려지는 컨테이너**(`AppOptions.BuildImage`, work dir bind-mount at `/work`)에서 `dotnet build`로 프로젝트를 컴파일합니다 — 따라서 신뢰할 수 없는 사용자 MSBuild가 호스트에 도달할 수 없습니다. NuGet 복원은 공유 볼륨을 통해 빌드 간 캐시됩니다. Web 호스트는 Docker 소켓 액세스가 필요합니다.
- C# + Python 시작 템플릿은 `src/Nodes/Builder/Templates/`에 있습니다.

## 실행 및 백테스트

- **Instances** = TPH 상태 계층(`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). 전환 시 엔티티 교체(id 변경), 컨테이너 ID는 유지됩니다.
- `NodeScheduler`가 최소 부하의 적합한 노드를 선택; `ContainerDispatcherFactory`가 원격 노드 HTTP 에이전트 또는 로컬 Docker 디스패처로 라우팅합니다.
- 완료 폴러가 종료된 컨테이너를 조정합니다(백테스트 컨테이너는 `--exit-on-stop`으로 자체 종료); 보고서 존재 → 완료(저장 `ReportJson`), 누락 → 실패.
- 라이브 컨테이너 로그는 SignalR을 통해 브라우저로 스트리밍; 백테스트 equity 곡선은 보고서에서 파싱되어 차트로 표시됩니다.

## cTrader Console CLI 참고

백테스트에는 `--data-mode`(기본값 `m1`), 날짜는 `dd/MM/yyyy HH:mm`, `params.cbotset` JSON 위치 인수가 필요합니다; `run`은 `--data-dir`를 거부합니다(백테스트 전용). `ContainerCommandHelpers` 참조.

## 노드 및 스케일링

실행 용량은 노드 에이전트를 추가하여 스케일합니다(자가 등록 + 하트비트). [노드 검색](../operations/node-discovery.md) 및 [스케일링](../deployment/scaling.md) 참조.

## 거래 계정이 필요합니다

cBot 실행 또는 백테스트에는 연결할 cTrader 거래 계정이 필요합니다. **거래 계정**에서 추가할 때까지 **새 cBot 실행** / **새 cBot 백테스트** 버튼은 비활성화되어 있습니다(도구 설명 포함)며 페이지에 계정 설정으로 연결되는 안내가 표시됩니다 — 더 이상 계정 없는 봇의 원시 `stream connect failed` 오류가 발생하지 않습니다.
