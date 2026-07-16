---
description: "cTrader cBots (C# 및 Python, 모두 .NET)를 브라우저 내 Monaco IDE에서 빌드, 실행, 백테스트하고, 공식 ghcr.io/spotware/ctrader-console 이미지에서 실행합니다."
---

# cBots 빌드 및 백테스트

cTrader cBots (C# **및** Python, 모두 .NET)를 브라우저 내 Monaco IDE에서 빌드, 실행, 백테스트하고,
공식 `ghcr.io/spotware/ctrader-console` 이미지에서 실행합니다.

## 빌드

- **Builder** 페이지는 Monaco 편집기를 호스팅합니다. `CBotBuilder`는 **일회용 컨테이너**에서
  `dotnet build`를 사용하여 프로젝트를 컴파일합니다 (`AppOptions.BuildImage`, 작업 디렉터리는 `/work`에 바인드 마운트됨).
  신뢰할 수 없는 사용자 MSBuild 대상이 호스트에 도달하지 않도록 합니다. NuGet 복원은 공유 볼륨을 통해 빌드 간에 캐시됩니다.
  웹 호스트는 Docker 소켓 접근 권한이 필요합니다.
- C# + Python 스타터 템플릿은 `src/Nodes/Builder/Templates/`에 있습니다.

## 실행 및 백테스트

- **Instances** = TPH 상태 계층 (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). 전환은 엔티티를 교체합니다 (ID 변경).
  컨테이너 ID는 유지됩니다.
- `NodeScheduler`는 가장 부하가 적은 적격 노드를 선택합니다. `ContainerDispatcherFactory`는
  원격 노드 HTTP 에이전트 또는 로컬 Docker 디스패처로 라우팅합니다.
- 완료 폴러는 종료된 컨테이너를 조정합니다 (백테스트 컨테이너는 `--exit-on-stop`을 통해 자동으로 종료됨).
  보고서가 있음 → 완료됨 (`ReportJson` 저장), 없음 → 실패함.
- 라이브 컨테이너 로그는 SignalR을 통해 브라우저로 스트림됩니다. 백테스트 자산곡선은 보고서에서 구문 분석되고 차트로 표시됩니다.

## 백테스트 마켓 데이터는 계정별로 캐시됩니다

cTrader Console은 역사적 틱/봉 데이터를 `--data-dir`에 다운로드합니다. 이 디렉터리는
**거래 계정별(계정 번호)로 키가 지정된 안정적이고 영구적인 캐시**입니다. 노드의 디스크에서
해당 컨테이너 경로(`/mnt/data`)에 바인드 마운트됩니다. 이는 인스턴스별 작업 디렉터리와
**별도의 중첩되지 않은 마운트**입니다. 따라서 동일한 계정의 모든 백테스트는 **이미 다운로드된 데이터를 재사용**하여
각 실행마다 데이터를 다시 다운로드하지 않습니다. (이전에는 데이터 디렉터리가 인스턴스별 작업 디렉터리 아래에 있었는데,
해당 ID가 실행할 때마다 변경되어 각 백테스트마다 새로운 다운로드를 강제했습니다.) 임시 인스턴스별 작업 디렉터리는
여전히 알고, 매개 변수, 암호 및 보고서를 포함합니다. 공유 데이터 캐시는 노드의 백테스트 데이터 사용량에 계산되며
노드 정리 작업으로 지워집니다.

## 백테스트 설정

**Backtest** 대화상자는 사용자가 조정 가능한 cTrader Console 백테스트 설정을 노출하므로
명령줄에 건드릴 필요가 없습니다:

- **Symbol / Timeframe** — 시간 프레임은 **모든 cTrader 기간의 드롭다운**입니다 (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, Renko/Range/Heikin 기간).
  콘솔의 표준 대소문자로 표기되므로 항상 유효한 `--period`를 선택합니다.
- **From / To** — 백테스트 창 (`--start` / `--end`).
- **Data mode** — 세 가지 cTrader 모드 중 하나 (`--data-mode`): **Tick data** (`tick`, 정확),
  **m1 bars** (`m1`, 빠름), 또는 **Open prices only** (`open`, 가장 빠름).
- **Starting balance** — 기본값은 `10000`입니다 (`--balance`). **0 잔액은 거래를 하지 않으며
  cTrader가 빈 보고서를 내보내고 이는 충돌합니다** ("Message expected"), 따라서 항상 0이 아닌 잔액이 전송됩니다.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **포인트 단위의 숫자 필드로 0 아래로 갈 수 없습니다**. **Tick data 모드에서는 숨겨집니다**.
  cTrader가 틱 데이터에서 스프레드를 파생합니다 (보낼 `--spread`가 없습니다).

데이터 디렉터리 (`--data-file` / `--data-dir`)는 앱 자체에서 관리됩니다 (계정별 캐시, 위 참조).
대화상자에 노출되지 않습니다.

:::note cTrader가 빈 백테스트에서 충돌
백테스트가 **결과를 생성하지 않으면** — 거래가 없거나 선택한 날짜/심볼에 대한 마켓 데이터가 없으면 —
cTrader Console의 자체 보고서 작성기는 `Message expected`를 throw하고 보고서 없이 종료합니다.
앱은 그 업스트림 버그를 수정할 수 없지만 이를 감지하고 인스턴스를 **Failed**로 표시합니다.
원시 스택 추적 대신 실행 가능한 이유가 있습니다 ("선택한 범위에 대한 백테스트 결과 없음…").
사용 가능한 마켓 데이터가 있는 더 넓은 날짜 범위를 선택하고 다시 시도하세요.
:::

## 인스턴스 상세 페이지

인스턴스 (`/instance/{id}`)를 열면 라이브 상태, 로그, 그리고 백테스트의 경우 자산곡선을 표시합니다.
**브라우저 탭 제목**은 특정 인스턴스를 반영합니다 (**cBot 이름 · 종류 · 심볼**, 예: `TrendBot · Backtest · EURUSD`).
따라서 라이브 실행 탭과 백테스트 탭을 한 눈에 구분할 수 있습니다.
실행과 동일한 cBot의 백테스트는 서로 다른 **lineages**로 추적됩니다 (상태 전환 간에 유지되는 안정적인 lineage ID).
따라서 페이지는 정확히 하나의 인스턴스를 따르고 실행의 데이터를 백테스트의 데이터와 혼합하지 않습니다.

## 인스턴스 수명 주기 컨트롤

각 인스턴스 행 (및 상세 페이지)에는 상태 정확 컨트롤이 있습니다. **활성** 인스턴스는
**Stop**을 표시합니다. **터미널** (Stopped / Completed / Failed) 인스턴스는 **Start (▶)** 를 표시하여
동일한 cBot, 계정, 심볼, 시간 프레임, ParamSet 및 이미지로 다시 시작할 수 있습니다
(실행은 실행으로 다시 시작되고, 백테스트는 백테스트로).
Stop을 클릭하면 "Stopping…" 알림이 표시되고 해결될 때까지 아이콘이 비활성화됩니다.
새로 생성된 실행은 목록에 즉시 나타납니다. 페이지 새로고침이 필요하지 않습니다.

콘솔 로그는 **인스턴스가 종료될 때 영구적으로 저장됩니다** — 실행 (Stop 시) 및
**백테스트** (완료 시) 모두 — 따라서 마지막 실행의 로그는 상세 페이지에서 계속 볼 수 있습니다.
그리고 로그 도구 모음을 통해 **클립보드에 복사**하거나 (**Copy logs** 아이콘) **다운로드**할 수 있습니다 (**Download logs** 아이콘).
컨테이너가 없어진 후에도 둘 다 작동합니다. 둘 다 화면상 tail이 아닌 인스턴스의 전체 콘솔 로그를 작동합니다.

업로드된 `.algo`는 여기에서 빌드되지 않았으므로 cBots 페이지의 **Last Build** 열은 비어있습니다
(브라우저에서 빌드한 cBots의 경우에만 빌드 시간을 표시합니다).

## 중지된 인스턴스 편집 및 재실행

**중지된** 인스턴스 (실행 또는 백테스트)에는 **Edit** 컨트롤이 있습니다 — 목록의 행에 있는 아이콘 **그리고**
상세 페이지의 Start/Stop 옆에 — 현재 설정으로 **미리 채워진** 대화상자를 열 수 있습니다.
**거래 계정, 심볼, 시간 프레임, ParamSet 및 이미지 태그**를 변경할 수 있습니다 (그리고 백테스트의 경우,
**창과 위의 모든 백테스트 설정**). 그 다음 **Save & start**는 새 설정으로 재실행합니다 (중지된 인스턴스를 교체).
컨트롤은 **인스턴스가 활성인 동안 비활성화됩니다** — 중지된 인스턴스만 편집할 수 있습니다.

## 코드 편집기에서 실행

코드 편집기에서 **Run**을 클릭하면 블라인드 하드코드 실행을 발생시키는 대신 대화상자를 엽니다:

- **Trading account** (필수) — cBot이 연결하는 cTrader 계정.
- **Parameter set** (선택사항) — 기존 집합을 선택하거나, 비워서 cBot의
  **기본 매개 변수 값**으로 실행하세요. 선택기 옆의 **+** 버튼은 새 ParamSet을 인라인으로 생성합니다
  (아래 참조) 그리고 이를 선택합니다.
- **Symbol / Timeframe**은 `EURUSD` / `h1`로 기본값을 설정하고 변경할 수 있습니다. **Cancel** 또는 **Run**.

**Run**에서 편집기는 현재 소스를 저장 + 빌드하고, 선택한 계정에서 선택한 매개 변수로 인스턴스를 시작한 후,
라이브 컨테이너 로그를 테일합니다. (로그 스트림은 서명된 사용자의 인증 쿠키를 `/hubs/logs` SignalR 허브로 전달하므로
`Invalid negotiation response received`로 실패하는 대신 연결됩니다.)

## ParamSet

**ParamSet**은 각 매개 변수 이름을 스칼라 값에 매핑하는 평면 JSON 객체로 저장된
명명된 재사용 가능한 cBot 매개 변수 재정의 집합입니다. 예: `{"Period": 14, "Label": "trend"}`.
실행/백테스트 시간에 이는 cTrader `params.cbotset` 파일로 변환됩니다
(`{ "Parameters": { … } }`). cBot의 **Parameter sets** 대화상자에서
또는 Run 대화상자에서 인라인으로 집합을 생성/편집할 수 있습니다.

모든 ParamSet은 **cBot에 속합니다**: New Parameter Set 대화상자는 모든 cBots를 나열합니다.
**반드시 하나를 선택해야 합니다** — 생성은 cBot이 선택될 때까지 차단됩니다. 집합의
**이름은 cBot별로 고유합니다**: 같은 cBot의 다른 집합이 이미 사용 중인 이름으로 집합을 생성하거나 이름을 바꾸면
거부됩니다 (대화상자의 명확한 오류, API의 `409 Conflict`). 같은 이름은 **다른** cBot에서 재사용할 수 있습니다.

JSON은 **저장할 때 검증됩니다**: 모든 값이 스칼라 (string / number / bool)인 단일 평면 객체여야 합니다.
non-object root, array, nested object, `null` value, 또는 형식 잘못된 JSON은 거부됩니다
(대화상자의 명확한 오류, API의 `400 Bad Request`). 빈 객체 `{}`는 허용되며 "재정의 없음"을 의미합니다.

## cTrader Console CLI 참고 사항

백테스트는 `--data-mode` (기본값 `m1`)이 필요하고, 날짜는 `dd/MM/yyyy HH:mm`로,
그리고 `params.cbotset` JSON 위치 인자가 필요합니다. `run`은 `--data-dir`을 거부합니다 (백테스트 전용).
`ContainerCommandHelpers`를 참조하세요.

## 노드 및 규모

실행 용량은 노드 에이전트를 추가하여 확장됩니다 (자체 등록 + 하트비트). 자세한 내용은
[node discovery](../operations/node-discovery.md) 및 [scaling](../deployment/scaling.md)을 참조하세요.

## 거래 계정이 필요합니다

cBot을 실행하거나 백테스트하려면 연결할 cTrader 거래 계정이 필요합니다. **Trading accounts** 아래에 추가할 때까지
**Run New cBot** / **Backtest New cBot** 버튼은 비활성화됩니다 (툴팁 포함).
페이지는 계정 설정으로 연결하는 프롬프트를 표시합니다 — 더 이상 계정이 없는 봇에서 원시 `stream connect failed` 오류가 발생하지 않습니다.
