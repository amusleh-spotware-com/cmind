---
description: "브라우저 내 Monaco IDE에서 cTrader cBot(C# 및 Python, 모두 .NET)을 빌드, 실행, 백테스트하고, 공식 ghcr.io/spotware/ctrader-console 이미지에서 실행합니다."
---

# Build & backtest cBots

브라우저 내 Monaco IDE에서 cTrader cBot(C# **및** Python, 모두 .NET)을 빌드, 실행, 백테스트하고, 공식 `ghcr.io/spotware/ctrader-console` 이미지에서 실행합니다.

## Build

- **Builder** 페이지는 Monaco 편집기를 호스팅합니다. `CBotBuilder`는 **일회용 컨테이너**에서(`AppOptions.BuildImage`, 작업 디렉토리 바인드 마운트 `/work`에서) `dotnet build`로 프로젝트를 컴파일하므로, 신뢰할 수 없는 사용자 MSBuild는 호스트에 접근할 수 없습니다. NuGet 복원은 공유 볼륨을 통해 빌드 간에 캐시됩니다. 웹 호스트는 Docker 소켓 접근이 필요합니다.
- C# 및 Python 스타터 템플릿은 `src/Nodes/Builder/Templates/`에 있습니다.

## Run & backtest

- **Instances** = TPH 상태 계층 구조(`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). 전환은 엔티티를 교체합니다(id 변경), 컨테이너 id는 유지됩니다.
- `NodeScheduler`는 로드가 가장 낮은 적합한 노드를 선택합니다. `ContainerDispatcherFactory`는 원격 노드 HTTP 에이전트 또는 로컬 Docker 디스패처로 라우팅합니다.
- 완료 폴러는 종료된 컨테이너를 조정합니다(백테스트 컨테이너는 `--exit-on-stop`을 통해 자체적으로 종료됨). 리포트 있음 → 완료됨(`ReportJson` 저장), 없음 → 실패함.
- 라이브 컨테이너 로그는 SignalR 을 통해 브라우저로 스트리밍됩니다. 백테스트 equity curves는 리포트에서 구문 분석되고 차트로 표시됩니다.

## Backtest market data is cached per account

cTrader Console은 과거 tick/bar 데이터를 `--data-dir`로 다운로드합니다. 이 디렉토리는 **트레이딩 계정**(계정 번호)을 키로 하는 **안정적이고 영구적인 캐시**이며, 노드의 디스크에서 자신의 컨테이너 경로(`/mnt/data`)로 바인드 마운트되며, 인스턴스별 작업 디렉토리로부터 **별도의 중첩되지 않은 마운트**입니다. 따라서 동일한 계정에 대한 모든 백테스트는 각 실행마다 다시 다운로드하는 대신 이미 다운로드한 데이터를 **재사용**합니다. (이전에는 데이터 디렉토리가 인스턴스별 작업 디렉토리 아래에 있었고, 이 id가 모든 실행마다 변경되어 매번 새로운 다운로드를 강제했습니다.) 임시 인스턴스별 작업 디렉토리는 여전히 알고리즘, 매개변수, 암호 및 리포트를 보관하고 있으며, 공유 데이터 캐시는 노드의 백테스트 데이터 사용량에 포함되고 노드 정리 작업으로 삭제됩니다.

## Backtest settings

**Backtest** 다이얼로그는 사용자가 조정 가능한 cTrader Console 백테스트 설정을 노출하므로, 명령줄을 건드릴 필요가 없습니다:

- **Symbol / Timeframe** — 타임프레임은 **모든 cTrader 기간의 드롭다운**입니다(`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, Renko/Range/Heikin 기간), 콘솔의 정규 대소문자로 표시되므로, 항상 유효한 `--period`를 선택할 수 있습니다.
- **From / To** — 백테스트 윈도우(`--start` / `--end`).
- **Data mode** — 3가지 cTrader 모드 중 하나(`--data-mode`): **Tick data**(`tick`, 정확함), **m1 bars**(`m1`, 빠름), 또는 **Open prices only**(`open`, 가장 빠름).
- **Starting balance** — `10000`으로 기본 설정됩니다(`--balance`). **0 잔액은 트레이드를 전혀 하지 않으며 cTrader가 빈 리포트를 발생시킵니다("Message expected"로 충돌)**, 따라서 0이 아닌 잔액은 항상 전송됩니다.
- **Commission** 및 **Spread** — `--commission` / `--spread`(스프레드는 pips 단위).

데이터 디렉토리(`--data-file` / `--data-dir`)는 앱 자체에서 관리됩니다(계정별 캐시, 위 참조), 다이얼로그에서 노출되지 않습니다.

## Instance detail page

인스턴스를 열면(`/instance/{id}`) 라이브 상태, 로그 및 백테스트의 경우 equity curve가 표시됩니다. **브라우저 탭 제목**은 특정 인스턴스(**cBot 이름 · 종류 · 심볼**, 예: `TrendBot · Backtest · EURUSD`)를 반영하므로, 라이브 실행 탭과 백테스트 탭을 한눈에 구별할 수 있습니다. 동일한 cBot의 실행과 백테스트는 별개의 **lineages**로 추적됩니다(상태 전환 전반에 걸쳐 유지되는 안정적인 lineage id). 따라서 페이지는 정확히 하나의 인스턴스를 따르고 실행 데이터와 백테스트 데이터를 절대 혼합하지 않습니다.

## Instance lifecycle controls

각 인스턴스 행(및 상세 페이지)에는 상태별로 올바른 제어 기능이 있습니다. **활성** 인스턴스는 **Stop**을 표시합니다. **터미널** 인스턴스(Stopped / Completed / Failed)는 **Start (▶)**를 표시하여 동일한 cBot, 계정, 심볼, 타임프레임, ParamSet 및 이미지로 다시 시작할 수 있습니다(실행은 실행으로 다시 시작되고, 백테스트는 백테스트로 다시 시작됩니다). Stop을 클릭하면 "Stopping…" 공지가 표시되고 해결될 때까지 아이콘이 비활성화되며, 새로 생성된 실행이 즉시 목록에 표시됩니다. 페이지 새로고침이 필요하지 않습니다.

콘솔 로그는 **인스턴스가 종료될 때 유지됩니다** — 실행(Stop 시)과 **백테스트**(완료 시) 모두에 대해 — 따라서 마지막 실행의 로그는 상세 페이지에서 계속 볼 수 있으며, 로그 도구 모음을 통해 **클립보드에 복사**됩니다(Copy logs 아이콘) 또는 **다운로드**됩니다(Download logs 아이콘). 컨테이너가 사라진 후에도 화면 탭뿐만 아니라 인스턴스의 전체 콘솔 로그에 대해 작동합니다.

**업로드된** `.algo`는 여기에서 빌드되지 않았으므로, cBots 페이지의 **Last Build** 열은 공백으로 남습니다(브라우저에서 빌드한 cBots에 대해서만 빌드 시간을 표시합니다).

## Edit & re-run a stopped instance

**중지된** 인스턴스(실행 또는 백테스트)는 **Edit** 제어 기능이 있습니다 — 목록의 행에 대한 아이콘 **및** 상세 페이지의 Start/Stop 옆에 있는 아이콘이며, 현재 구성으로 **미리 채워진** 다이얼로그를 엽니다. **트레이딩 계정, 심볼, 타임프레임, ParamSet 및 이미지 태그**를 변경할 수 있으며(그리고 백테스트의 경우, **윈도우 및 위의 모든 백테스트 설정**), **Save & start**를 누르면 새 설정으로 다시 시작합니다(중지된 인스턴스를 교체). 이 제어 기능은 **인스턴스가 활성인 동안 비활성화됩니다** — 중지된 인스턴스만 편집할 수 있습니다.

## Run from the code editor

코드 편집기에서 **Run**을 클릭하면 즉시 하드코딩된 실행을 수행하는 대신 다이얼로그가 열립니다:

- **Trading account**(필수) — cBot이 연결할 cTrader 계정.
- **Parameter set**(선택 사항) — 기존 집합을 선택하거나, cBot의 **기본 매개변수 값**으로 실행하려면 비워 둡니다. 선택기 옆의 **+** 버튼은 새 ParamSet을 인라인으로 생성합니다(아래 참조) 및 선택합니다.
- **Symbol / Timeframe**은 기본값으로 `EURUSD` / `h1`이며 변경할 수 있습니다. **Cancel** 또는 **Run**.

**Run**에서 편집기는 현재 소스를 저장 및 빌드하고, 선택한 계정의 선택한 매개변수로 인스턴스를 시작한 다음 라이브 컨테이너 로그를 추적합니다. (로그 스트림은 로그인한 사용자의 인증 쿠키를 `/hubs/logs` SignalR 허브로 전달하므로 `Invalid negotiation response received`로 실패하는 대신 연결됩니다.)

## Parameter sets

**ParamSet**은 각 매개변수 이름을 스칼라 값에 매핑하는 flat JSON 객체로 저장된 명명된 재사용 가능한 cBot 매개변수 재정의 집합입니다. 예: `{"Period": 14, "Label": "trend"}`. 실행/백테스트 시간에 cTrader `params.cbotset` 파일로 변환됩니다(`{ "Parameters": { … } }`). cBot의 **Parameter sets** 대화상자에서 집합을 raw JSON으로 생성/편집하거나 Run 대화상자에서 인라인으로 생성할 수 있습니다.

모든 ParamSet은 **cBot에 속합니다**: 새 ParamSet 대화상자는 모든 cBot을 나열하고 **하나를 선택해야 합니다** — cBot이 선택될 때까지 생성이 차단됩니다. 집합의 **이름은 cBot별로 고유합니다**: 집합을 생성하거나 동일한 cBot의 다른 집합이 이미 사용하는 이름으로 이름을 바꾸면 거부됩니다(대화상자의 명확한 오류, API의 `409 Conflict`). 동일한 이름은 **다른** cBot에서 재사용될 수 있습니다.

JSON은 **저장 시 검증됩니다**: 단일 flat 객체여야 하며 모든 값은 스칼라(string / number / bool)여야 합니다. 비객체 루트, 배열, 중첩된 객체, `null` 값 또는 잘못된 JSON은 거부됩니다(대화상자의 명확한 오류, API의 `400 Bad Request`). 빈 객체 `{}`은 허용되며 "재정의 없음"을 의미합니다.

## cTrader Console CLI notes

백테스트는 `--data-mode`(기본값 `m1`), 날짜를 `dd/MM/yyyy HH:mm` 형식으로, 그리고 `params.cbotset` JSON 위치 인수가 필요합니다. `run`은 `--data-dir`을 거부합니다(백테스트 전용). `ContainerCommandHelpers`를 참조하세요.

## Nodes & scale

실행 용량은 Node 에이전트를 추가하여 확장됩니다(자체 등록 및 하트비트). [node discovery](../operations/node-discovery.md) 및 [scaling](../deployment/scaling.md)을 참조하세요.
## A trading account is required

cBot을 실행하거나 백테스트하려면 연결할 cTrader 트레이딩 계정이 필요합니다. **Trading accounts**에서 계정을 추가할 때까지, **Run New cBot** / **Backtest New cBot** 버튼은 비활성화됩니다(도구 설명 포함) 및 페이지는 계정 설정으로 연결되는 프롬프트를 표시합니다 — 더 이상 계정이 없는 봇의 raw `stream connect failed` 오류가 발생하지 않습니다.
