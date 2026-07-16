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

## 코드 편집기에서 실행

코드 편집기에서 **실행**을 클릭하면 하드코딩된 무작정 실행 대신 대화 상자가 열립니다:

- **거래 계정**(필수) — cBot이 연결되는 cTrader 계정.
- **파라미터 세트**(선택) — 기존 세트를 선택하거나, 비워 두면 cBot의 **기본 파라미터 값**으로 실행합니다. 선택기 옆의 **+** 버튼은 새 파라미터 세트를 인라인으로 생성하고(아래 참조) 선택합니다.
- **심볼 / 시간대**는 기본값이 `EURUSD` / `h1`이며 변경할 수 있습니다. **취소** 또는 **실행**.

**실행** 시 편집기는 현재 소스를 저장하고 빌드한 다음, 선택한 계정에서 선택한 파라미터로 인스턴스를 시작하고 실시간 컨테이너 로그를 따라갑니다. (로그 스트림은 로그인한 사용자의 인증 쿠키를 SignalR 허브 `/hubs/logs`로 전달하므로 `Invalid negotiation response received`로 실패하는 대신 연결됩니다.)

## 파라미터 세트

**파라미터 세트**는 이름이 지정된 재사용 가능한 cBot 파라미터 재정의 세트로, 각 파라미터 이름을 스칼라 값에 매핑하는 평면 JSON 객체로 저장됩니다(예: `{"Period": 14, "Label": "trend"}`). 실행/백테스트 시 cTrader `params.cbotset` 파일(`{ "Parameters": { … } }`)로 변환됩니다. cBot의 **파라미터 세트** 대화 상자에서 원시 JSON으로, 또는 실행 대화 상자에서 인라인으로 세트를 생성/편집할 수 있습니다.

JSON은 저장 시 **검증**됩니다. 모든 값이 스칼라(문자열 / 숫자 / 불리언)인 단일 평면 객체여야 합니다. 객체가 아닌 루트, 배열, 중첩 객체, `null` 값 또는 잘못된 형식의 JSON은 거부됩니다(대화 상자에 명확한 오류, API에서 `400 Bad Request`). 빈 객체 `{}`는 허용되며 "재정의 없음"을 의미합니다.

## 인스턴스 수명 주기 컨트롤

각 인스턴스 행(및 세부 정보 페이지)에는 상태에 맞는 컨트롤이 있습니다. **활성** 인스턴스에는 **중지**가 표시되고, **종료된**(중지됨 / 완료됨 / 실패) 인스턴스에는 동일한 cBot, 계정, 심볼, 시간대, 파라미터 세트, 이미지로 다시 실행하는 **시작(▶)**이 표시됩니다(실행은 실행으로, 백테스트는 백테스트로 다시 시작). 중지를 클릭하면 "중지 중…" 알림이 표시되고 완료될 때까지 아이콘이 비활성화됩니다. 새로 만든 실행은 페이지를 새로 고치지 않고도 즉시 목록에 나타납니다.

콘솔 로그는 **인스턴스가 종료될 때 유지**됩니다. 실행(중지 시)과 **백테스트**(완료 시) 모두 마찬가지이므로, 마지막 실행의 로그는 세부 정보 페이지에서 계속 볼 수 있고 컨테이너가 사라진 후에도 **로그 다운로드** 아이콘으로 다운로드할 수 있습니다.
