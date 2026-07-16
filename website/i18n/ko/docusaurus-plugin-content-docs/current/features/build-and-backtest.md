---
description: "cTrader cBots (C# 및 Python, 모두 .NET)를 브라우저 내 Monaco IDE에서 빌드, 실행, 백테스트 및 공식 ghcr.io/spotware/ctrader-console 이미지에서 실행합니다."
---

# Build & backtest cBots

cTrader cBots(C# **및** Python, 모두 .NET)를 브라우저 내 Monaco IDE에서 빌드, 실행, 백테스트하고 공식
`ghcr.io/spotware/ctrader-console` 이미지에서 실행합니다.

## Build

- **Builder** 페이지는 Monaco 에디터를 호스팅합니다. `CBotBuilder`는 **일회용 컨테이너**에서
  `dotnet build`를 통해 프로젝트를 컴파일합니다 (`AppOptions.BuildImage`, 작업 디렉토리는 `/work`에 바인드 마운트됨).
  신뢰할 수 없는 사용자 MSBuild 타겟이 호스트에 접근할 수 없도록 합니다. NuGet 복원은 공유 볼륨을 통해 빌드 간에
  캐시됩니다. 웹 호스트는 Docker 소켓 접근이 필요합니다.
- C# + Python 스타터 템플릿은 `src/Nodes/Builder/Templates/`에 있습니다.

## Run & backtest

- **Instances** = TPH 상태 계층 구조 (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). 전환은 엔티티를 대체합니다(id 변경).
  컨테이너 id는 유지됩니다.
- `NodeScheduler`는 가장 부하가 낮은 적합한 노드를 선택합니다. `ContainerDispatcherFactory`는
  원격 노드 HTTP 에이전트 또는 로컬 Docker 디스패처로 라우팅합니다.
- 완료 폴러는 종료된 컨테이너를 조정합니다 (백테스트 컨테이너는 `--exit-on-stop`을 통해 자동 종료됨).
  리포트가 있으면 완료됨 (ReportJson 저장), 없으면 실패입니다.
- 라이브 컨테이너 로그는 SignalR을 통해 브라우저로 스트리밍됩니다. 백테스트 에퀴티 곡선은
  리포트에서 파싱되어 차트로 표시됩니다.

## Backtest market data is cached per account

cTrader Console은 과거 틱/봉 데이터를 해당 `--data-dir`에 다운로드합니다. 해당 디렉토리는
**거래 계정(계정 번호)으로 인덱싱된 안정적인 영구 캐시**입니다 — 노드의 디스크에서 자신의 컨테이너 경로(`/mnt/data`)로
바인드 마운트되며, 인스턴스별 작업 디렉토리와는 **별개의 중첩되지 않은 마운트**입니다. 따라서 동일한 계정에서 모든 백테스트는
**이미 다운로드된 데이터를 재사용**하므로 매 실행마다 다시 다운로드하지 않습니다. (이전에는
데이터 디렉토리가 인스턴스별 작업 디렉토리 아래에 있었는데, 이는 매 실행마다 id가 변경되어 매 백테스트마다 새로운 다운로드를 강제했습니다.) 임시 인스턴스별 작업 디렉토리는 여전히 알고, 파라미터, 비밀번호
및 리포트를 보유합니다. 공유 데이터 캐시는 노드의 백테스트 데이터 사용량으로 계산되며 노드 정리 작업으로 정리됩니다.

## Backtest settings

**Backtest** 다이알로그는 cTrader Console 백테스트 CLI가 허용하는 모든 설정을 노출하므로
명령줄에 접근할 필요가 없습니다:

- **From / To** — 백테스트 기간 (`--start` / `--end`).
- **Data mode** — 세 가지 cTrader 모드 중 하나 (`--data-mode`): **Tick data** (`tick`, 정확),
  **m1 bars** (`m1`, 빠름), 또는 **Open prices only** (`open`, 가장 빠름).
- **Starting balance** — 기본값은 `10000` (`--balance`). **0 잔액은 거래를 실행하지 않으며 cTrader가
  빈 리포트를 내보낸 후 충돌** ("Message expected")하므로 항상 0이 아닌 잔액이 전송됩니다.
- **Commission** 및 **Spread** — `--commission` / `--spread` (스프레드는 핍 단위).
- **Data file** (선택사항) — 노드 측 경로의 과거 데이터 파일 (`--data-file`). 다운로드된/캐시된 데이터를
  사용하려면 비워 둡니다.
- **Expose environment variables** — 호스트 환경 변수를 cBot에 전달하는 토글
  (`--environment-variables` 플래그).

## Instance detail page

인스턴스(`/instance/{id}`)를 열면 라이브 상태, 로그, 그리고 백테스트의 경우 에퀴티 곡선이 표시됩니다. **브라우저 탭 제목**은
특정 인스턴스(**cBot 이름 · 유형 · 심볼**, 예: `TrendBot · Backtest · EURUSD`)를 반영하므로 라이브 실행 탭과 백테스트 탭을
한눈에 구분할 수 있습니다. 동일한 cBot의 실행과 백테스트는 별개의 **라인리지**(상태 전환을 통해 유지되는 안정적인 라인리지 id)로 추적되므로,
페이지는 정확히 하나의 인스턴스를 따르며 실행의 데이터와 백테스트를 혼합하지 않습니다.

## Instance lifecycle controls

각 인스턴스 행(및 해당 세부 정보 페이지)에는 상태별 올바른 컨트롤이 있습니다. **활성** 인스턴스는
**Stop**을 표시합니다. **터미널** 인스턴스(Stopped / Completed / Failed)는 **Start (▶)**를 표시하여
동일한 cBot, 계정, 심볼, 타임프레임, 파라미터 세트 및 이미지로 다시 시작합니다(실행은 실행으로 다시 시작되고, 백테스트는
백테스트로 다시 시작됩니다). Stop을 클릭하면 "Stopping…" 통지가 표시되고 아이콘이 비활성화되며 해결될 때까지 비활성화되므로, 새로 생성된 실행이
목록에 즉시 나타납니다 — 페이지를 다시 로드할 필요가 없습니다.

콘솔 로그는 **인스턴스가 종료될 때 영구 저장**됩니다 — 실행 시(Stop 시) 및 **백테스트**(완료 시) 모두 — 따라서 마지막 실행의 로그는
세부 정보 페이지에 계속 표시되며, 로그 도구 모음을 통해 **클립보드로 복사** (로그 복사 아이콘) 또는 **다운로드** (로그 다운로드 아이콘)할 수
있습니다. 컨테이너가 사라진 후에도 모두 인스턴스의 전체 콘솔 로그에 작용하며, 화면상 꼬리만이 아닙니다.

**업로드된** `.algo`는 여기서 빌드되지 않았으므로 cBots 페이지의 **Last Build** 열은 비어 있습니다(브라우저에서 빌드한 cBots의 경우에만
빌드 시간을 표시합니다).

## Edit & re-run a stopped instance

**중지된** 인스턴스(실행 또는 백테스트)에는 **Edit** 컨트롤이 있습니다 — 목록의 행에 있는 아이콘 **그리고**
세부 정보 페이지의 Start/Stop 옆에 있는 아이콘 — 현재 구성으로 **미리 채워진** 다이알로그를 엽니다.
**거래 계정, 심볼, 타임프레임, 파라미터 세트 및 이미지 태그**를 변경할 수 있습니다 (그리고 백테스트의 경우,
**기간 및 위의 모든 백테스트 설정**), 그런 다음 **Save & start**는 새 설정으로 다시 시작합니다(중지된 인스턴스를 대체합니다).
컨트롤은 **인스턴스가 활성인 동안 비활성화**됩니다 — 중지된 인스턴스만 편집할 수 있습니다.

## Run from the code editor

코드 에디터에서 **Run**을 클릭하면 블라인드 하드코딩된 실행을 하는 대신 다이알로그를 엽니다:

- **Trading account** (필수) — cBot이 연결할 cTrader 계정입니다.
- **Parameter set** (선택사항) — 기존 세트를 선택하거나, cBot의 **기본 파라미터 값**으로 실행하려면 비워 둡니다.
  선택자 옆의 **+** 버튼은 새 파라미터 세트를 인라인으로 생성합니다(아래 참조) 및 선택합니다.
- **Symbol / Timeframe**은 기본값 `EURUSD` / `h1`이며 변경할 수 있습니다. **Cancel** 또는 **Run**.

**Run**에서 에디터는 현재 소스를 저장 + 빌드하고, 선택한 계정의 선택한 파라미터로 인스턴스를 시작한 다음, 라이브 컨테이너 로그를 추적합니다.
(로그 스트림은 로그인한 사용자의 인증 쿠키를 `/hubs/logs` SignalR 허브로 전달하므로 `Invalid negotiation response received` 오류가 발생하지 않고 연결됩니다.)

## Parameter sets

**Parameter set**은 각 파라미터 이름을 스칼라 값으로 매핑하는 플랫 JSON 객체(예: `{"Period": 14, "Label": "trend"}`)로 저장된 이름이 지정된 재사용 가능한 cBot 파라미터 오버라이드 세트입니다. 실행/백테스트 시간에는 cTrader `params.cbotset` 파일(`{ "Parameters": { … } }`)로 변환됩니다. cBot의 **Parameter sets** 다이알로그에서 raw JSON으로 세트를 생성/편집하거나 Run 다이알로그에서 인라인으로 생성할 수 있습니다.

모든 파라미터 세트는 **cBot에 속합니다**: 새 Parameter Set 다이알로그는 모든 cBots를 나열하고 **하나를 선택**해야 합니다 — cBot이 선택될 때까지 생성이 차단됩니다. 세트의 **이름은 cBot당 고유**합니다: 동일한 cBot의 다른 세트가 이미 사용 중인 이름으로 세트를 생성하거나 이름을 바꾸면 거부됩니다(다이알로그의 명확한 오류, API에서는 `409 Conflict`). 동일한 이름은 **다른** cBot에서 재사용할 수 있습니다.

JSON은 **저장 시 검증**됩니다: 단일 플랫 객체여야 하며 모든 값은 스칼라(문자열 / 숫자 / 부울)여야 합니다. 비객체 루트, 배열, 중첩 객체, `null` 값 또는 형식이 잘못된 JSON은 거부됩니다(다이알로그의 명확한 오류, API에서는 `400 Bad Request`). 빈 객체 `{}`는 허용되며 "오버라이드 없음"을 의미합니다.

## cTrader Console CLI notes

백테스트에는 `--data-mode` (기본값 `m1`), `dd/MM/yyyy HH:mm` 형식의 날짜 및
`params.cbotset` JSON 위치 인수가 필요합니다. `run`은 `--data-dir`을 거부합니다 (백테스트 전용).
`ContainerCommandHelpers`를 참조하세요.

## Nodes & scale

실행 용량은 노드 에이전트를 추가하여 확장합니다(자체 등록 + 하트비트). [node discovery](../operations/node-discovery.md)
및 [scaling](../deployment/scaling.md)을 참조하세요.

## A trading account is required

cBot을 실행하거나 백테스트하려면 연결할 cTrader 거래 계정이 필요합니다. **Trading accounts** 아래에서 계정을 추가할 때까지
**Run New cBot** / **Backtest New cBot** 버튼이 비활성화됩니다 (도움말 포함) 그리고 페이지는 계정 설정 링크로 안내합니다 —
더 이상 계정이 없는 봇에서 원시 `stream connect failed` 오류가 발생하지 않습니다.
