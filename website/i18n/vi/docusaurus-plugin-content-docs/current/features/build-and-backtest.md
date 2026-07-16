---
description: "Build, run, backtest cTrader cBots (C# và Python, cả hai .NET) từ in-browser Monaco IDE, chạy trên official ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **và** Python, cả hai .NET) từ in-browser Monaco IDE, chạy trên official `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile project với `dotnet build` **trong throwaway container** (`AppOptions.BuildImage`, work dir bind-mount tại `/work`), vì vậy untrusted user MSBuild targets không thể reach host. NuGet restore được cached across builds qua shared volume. Web host cần Docker socket access.
- C# + Python starter templates nằm trong `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Transition replace entity (id change), container id được carried over.
- `NodeScheduler` pick least-loaded eligible node; `ContainerDispatcherFactory` route tới remote node HTTP agent hoặc local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit qua `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Live container logs stream tới browser qua SignalR; backtest equity curves parsed từ report + charted.

## Backtest market data is cached per account

cTrader Console downloads historical tick/bar data vào `--data-dir` của nó. Directory đó là một **stable, persistent cache keyed on the trading account** (account number của nó) — bind-mounted từ node's disk tại path container của nó (`/mnt/data`), một **separate, non-nested mount** từ per-instance work dir. Vì vậy mỗi backtest trên cùng account **reuses** data đã được downloaded trước đó thay vì re-downloading nó mỗi lần chạy. (Trước đây data dir nằm dưới per-instance work dir, mà id thay đổi mỗi lần chạy, điều này forced một fresh download mỗi backtest.) Ephemeral per-instance work dir vẫn giữ algo, params, password và report; shared data cache được counted trong node's backtest-data usage và cleared bởi node-clean action.

## Backtest settings

**Backtest** dialog exposes mỗi setting mà cTrader Console backtest CLI accepts, vì vậy bạn không bao giờ phải touch command line:

- **From / To** — backtest window (`--start` / `--end`).
- **Data mode** — một trong ba cTrader modes (`--data-mode`): **Tick data** (`tick`, accurate), **m1 bars** (`m1`, fast), hoặc **Open prices only** (`open`, fastest).
- **Starting balance** — defaults tới `10000` (`--balance`). Một **0 balance places no trades và makes cTrader emit một empty report nó sau đó crashes trên** ("Message expected"), vì vậy non-zero balance luôn được sent.
- **Commission** và **Spread** — `--commission` / `--spread` (spread in pips).
- **Data file** (optional) — một node-side path tới historical data file (`--data-file`); để empty để use downloaded/cached data.
- **Expose environment variables** — một toggle mà passes host environment variables tới cBot (flag `--environment-variables`).

## Instance detail page

Opening một instance (`/instance/{id}`) shows live status, logs của nó và — cho một backtest — equity curve. **Browser tab title** reflects specific instance (**cBot name · kind · symbol**, ví dụ `TrendBot · Backtest · EURUSD`) vì vậy live-run tab và backtest tab là distinguishable tại first glance. Một run và một backtest của cùng cBot được tracked như distinct **lineages** (stable lineage id được carried across state transitions), vì vậy page follows chính xác một instance và never mixes run's data với backtest's.

## Instance lifecycle controls

Mỗi instance row (và detail page của nó) có state-correct controls. Một **active** instance shows **Stop**; một **terminal** (Stopped / Completed / Failed) shows **Start (▶)** để re-launch nó với cùng cBot, account, symbol, timeframe, parameter set và image (run restarts như run, backtest như backtest). Clicking Stop shows "Stopping…" notice và disables icon cho đến khi nó resolves, và newly created run appears trong list ngay — no page reload.

Console logs được **persisted khi một instance terminates** — cho run (trên Stop) và cho **backtest** (trên completion) giống nhau — vì vậy last run's logs stay viewable trên detail page và, qua log toolbar, **copied tới clipboard** (Copy logs icon) hoặc **downloaded** (Download logs icon) thậm chí sau khi container gone. Cả hai act trên instance's full console log, không chỉ on-screen tail.

Một **uploaded** `.algo` was never built tại đây, vì vậy **Last Build** column của nó trên cBots page được left blank (nó shows build time chỉ cho cBots mà bạn build trong browser).

## Edit & re-run a stopped instance

Một **stopped** instance (run hoặc backtest) có một **Edit** control — một icon trên row của nó trong list **và** bên cạnh Start/Stop trên detail page của nó — mà opens một dialog **prefilled** với configuration hiện tại của nó. Bạn có thể change **trading account, symbol, timeframe, parameter set và image tag** (và, cho backtest, **window và tất cả backtest settings** trên), sau đó **Save & start** re-launches nó với new settings (replacing stopped instance). Control là **disabled while instance active** — chỉ một stopped instance có thể được edited.

## Run from the code editor

Clicking **Run** trong code editor opens một dialog thay vì firing một blind, hard-coded run:

- **Trading account** (required) — cTrader account mà cBot connects tới.
- **Parameter set** (optional) — pick existing set, hoặc leave empty để run với cBot's **default parameter values**. Một **+** button bên cạnh selector creates một new parameter set inline (xem dưới) và selects nó.
- **Symbol / Timeframe** default tới `EURUSD` / `h1` và có thể changed; **Cancel** hoặc **Run**.

Trên **Run** editor saves + builds current source, starts instance trên chosen account với chosen parameters, sau đó tails live container logs. (Log stream forwards signed-in user's auth cookie tới `/hubs/logs` SignalR hub, vì vậy nó connects thay vì failing với `Invalid negotiation response received`.)

## Parameter sets

Một **parameter set** là một named, reusable set của cBot parameter overrides stored như flat JSON object mapping mỗi parameter name tới scalar value, ví dụ `{"Period": 14, "Label": "trend"}`. Tại run/backtest time nó được turned thành cTrader `params.cbotset` file (`{ "Parameters": { … } }`). Bạn có thể create/edit một set như raw JSON từ cBot's **Parameter sets** dialog hoặc inline từ Run dialog.

Mỗi parameter set **belongs tới một cBot**: New Parameter Set dialog lists tất cả cBots của bạn và bạn **must pick một** — creation được blocked cho đến khi cBot được selected. Một set's **name là unique per cBot**: creating hoặc renaming một set tới một name mà một set khác của cùng cBot đã uses là rejected (clear error trong dialog, `409 Conflict` tại API). Cùng name có thể được reused trên một **different** cBot.

JSON được **validated** trên save: nó phải là single flat object mà values của nó tất cả là scalars (string / number / bool). Một non-object root, một array, một nested object, một `null` value, hoặc malformed JSON là rejected (clear error trong dialog, `400 Bad Request` tại API). Một empty object `{}` được allowed và means "no overrides".

## cTrader Console CLI notes

Backtests cần `--data-mode` (default `m1`), dates như `dd/MM/yyyy HH:mm`, và `params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Xem `ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale bằng cách adding node agents (self-register + heartbeat). Xem [node discovery](../operations/node-discovery.md) và [scaling](../deployment/scaling.md).

## A trading account is required

Running hoặc backtesting một cBot cần một cTrader trading account để connect tới. Cho đến khi bạn thêm một dưới **Trading accounts**, **Run New cBot** / **Backtest New cBot** buttons được disabled (với tooltip) và page shows một prompt linking tới account setup — bạn không còn hit một raw `stream connect failed` error từ một bot mà không account.
