---
description: "인브라우저 Monaco IDE에서 cTrader cBot (C# 및 Python, 둘 다 .NET) 빌드, 실행, 백테스트, 공식 ghcr.io/spotware/ctrader-console 이미지에서 실행."
---

# cBot 빌드 및 백테스트

인브라우저 Monaco IDE에서 cTrader cBot (C# **및** Python, 둘 다 .NET) 빌드, 실행, 백테스트, 공식 `ghcr.io/spotware/ctrader-console` 이미지에서 실행.

## 빌드

- **빌더** 페이지는 Monaco 편집기를 호스팅합니다; `CBotBuilder`는 **일회용 컨테이너**에서 `dotnet build`를 사용하여 프로젝트를 컴파일합니다 (`AppOptions.BuildImage`, 작업 디렉토리는 `/work`에 바인드 마운트). 따라서 신뢰할 수 없는 사용자 MSBuild 대상이 호스트에 도달하지 않습니다. NuGet 복원은 공유 볼륨을 통해 빌드 전체에 캐시됩니다. Web 호스트는 Docker 소켓 액세스가 필요합니다.
- C# + Python 시작 템플릿은 `src/Nodes/Builder/Templates/`에 있습니다.

## 실행 및 백테스트

- **인스턴스** = TPH 상태 계층 (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). 전환이 엔티티를 교체 (ID 변경), 컨테이너 ID가 전달됨.
- `NodeScheduler`는 가장 로드가 적은 적격 노드를 선택; `ContainerDispatcherFactory`는 원격 노드 HTTP 에이전트 또는 로컬 Docker 디스패처로 라우팅합니다.
- 완료 폴러는 종료된 컨테이너를 조정 (백테스트 컨테이너는 `--exit-on-stop`을 통해 자체 종료); 보고서 표시 → 완료 (저장 `ReportJson`), 누락 → 실패.
- 라이브 컨테이너 로그는 SignalR을 통해 브라우저로 스트림; 백테스트 공정 곡선이 보고서에서 분석되고 차트로 표시됩니다.

## cTrader Console CLI 노트

백테스트는 `--data-mode` (기본값 `m1`), 날짜를 `dd/MM/yyyy HH:mm`로, `params.cbotset` JSON 위치 매개변수가 필요합니다; `run`은 `--data-dir` (백테스트만)을 거부합니다. `ContainerCommandHelpers`를 참조하세요.

## 노드 및 확장

실행 용량은 노드 에이전트를 추가하여 확장합니다 (자체 등록 + 하트비트). [노드 디스커버리](../operations/node-discovery.md) 및 [확장](../deployment/scaling.md)을 참조하세요.
