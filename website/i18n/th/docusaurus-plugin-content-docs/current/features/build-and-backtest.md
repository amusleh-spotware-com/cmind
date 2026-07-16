---
description: "สร้าง รัน ทดสอบย้อนหลัง cTrader cBots (C# และ Python ทั้งคู่ .NET) จากตัวแก้ไข Monaco ในเบราว์เซอร์ รัน บน official ghcr.io/spotware/ctrader-console image"
---

# Build & backtest cBots

สร้าง รัน ทดสอบย้อนหลัง cTrader cBots (C# **และ** Python ทั้งคู่ .NET) จากตัวแก้ไข Monaco ในเบราว์เซอร์ รัน บน official `ghcr.io/spotware/ctrader-console` image

## Build

- **Builder** page โฮสต์ตัวแก้ไข Monaco; `CBotBuilder` compile project ด้วย `dotnet build` **ในคอนเทนเนอร์ที่ชั่วคราว** (`AppOptions.BuildImage` work dir bind-mount ที่ `/work`) เพื่อไม่ให้ MSBuild targets ที่ไม่น่าเชื่อถือเข้าถึง host NuGet restore จะถูกแคชไว้ข้ามการ build ผ่าน shared volume Web host ต้องการ Docker socket access
- C# + Python starter templates อยู่ใน `src/Nodes/Builder/Templates/`

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`) Transition แทนที่ entity (id change) container id จะถูกพกพา
- `NodeScheduler` เลือก least-loaded eligible node; `ContainerDispatcherFactory` route ไปยัง remote node HTTP agent หรือ local Docker dispatcher
- Completion pollers reconcile exited containers (backtest containers self-exit ผ่าน `--exit-on-stop`); report present → completed (store `ReportJson`) missing → failed
- Live container logs stream ไปยังเบราว์เซอร์ผ่าน SignalR; backtest equity curves parsed จาก report + charted

## Backtest market data is cached per account

cTrader Console ดาวน์โหลด historical tick/bar data เข้าไป `--data-dir` ของมัน ไดเรกทอรีนั้นเป็น **stable persistent cache keyed บน trading account** (account number ของมัน) — bind-mounted จาก disk ของ node ที่ container path ของมัน (`/mnt/data`) **separate non-nested mount** จาก per-instance work dir ดังนั้นทุก backtest บน account เดียวกัน **reuses** ข้อมูลที่ดาวน์โหลดแล้ว แทนที่จะ re-download ในแต่ละครั้ง (ก่อนหน้านี้ data dir อยู่ใต้ per-instance work dir ซึ่ง id เปลี่ยนทุกครั้ง ที่บังคับให้ fresh download ทุก backtest) ephemeral per-instance work dir ยังคงเก็บ algo params password และ report; shared data cache นับรวมใน node's backtest-data usage และ cleared โดย node-clean action

## Backtest settings

**Backtest** dialog expose ทุก setting ที่ cTrader Console backtest CLI accept เพื่อคุณไม่ต้องสัมผัสคำสั่ง command line:

- **From / To** — backtest window (`--start` / `--end`)
- **Data mode** — `m1` (1-minute bars) หรือ `tick` (`--data-mode`)
- **Starting balance** — defaults ถึง `10000` (`--balance`) **0 balance places no trades และทำให้ cTrader emit empty report ที่มันจะ crash บน** ("Message expected") ดังนั้น non-zero balance จะถูกส่งเสมอ
- **Commission** และ **Spread** (`--commission` / `--spread` spread ใน pips)
- **Advanced options** — free-form `name=value` per line box สำหรับ backtest option อื่นที่ cTrader support (เช่น `applyCommissionAutomatically=true`); แต่ละ line กลายเป็น `--name value` CLI argument

## Instance detail page

เปิด instance (`/instance/{id}`) แสดง live status logs และ — สำหรับ backtest — equity curve **browser tab title** สะท้อน specific instance (**cBot name · kind · symbol** เช่น `TrendBot · Backtest · EURUSD`) เพื่อว่า live-run tab และ backtest tab จะแตกต่างได้ง่ายในแวบแรก run และ backtest ของ cBot เดียวกัน tracked as distinct **lineages** (stable lineage id พกพาข้าม state transitions) ดังนั้นหน้านี้ follow exactly one instance และไม่เคยผสมข้อมูล run's ด้วย backtest's

## Instance lifecycle controls

แต่ละ instance row (และ detail page ของมัน) มี state-correct controls **active** instance แสดง **Stop**; **terminal** one (Stopped / Completed / Failed) แสดง **Start (▶)** เพื่อ re-launch ด้วย cBot account symbol timeframe parameter set และ image เดียวกัน (run restarts as run backtest as backtest) Clicking Stop แสดง "Stopping…" notice และ disable icon จนกว่า resolve และ newly created run ปรากฏในรายการทันที — no page reload

Console logs เป็น **persisted เมื่อ instance terminates** — สำหรับ run (on Stop) และสำหรับ **backtest** (on completion) เช่นเดียวกัน — ดังนั้น last run's logs stay viewable บน detail page และผ่าน log toolbar **copied ไปยัง clipboard** (Copy logs icon) หรือ **downloaded** (Download logs icon) แม้หลังจากคอนเทนเนอร์หายไป ทั้งสองทำต่อ instance's full console log ไม่ใช่ on-screen tail เท่านั้น

**uploaded** `.algo` ไม่เคย built ที่นี่ ดังนั้น **Last Build** column ของมัน บน cBots page ว่าง (มันแสดง build time เฉพาะสำหรับ cBots ที่คุณ build ในเบราว์เซอร์)

## Edit & re-run a stopped instance

**stopped** instance (run หรือ backtest) มี **Edit** control — icon บน row ของมัน ในรายการ **และ** ข้าง Start/Stop บน detail page — ที่เปิด dialog **prefilled** ด้วย current configuration ของมัน คุณสามารถเปลี่ยน **trading account symbol timeframe parameter set และ image tag** (และสำหรับ backtest **window และทั้งหมด backtest settings** ด้านบน) แล้ว **Save & start** re-launch ด้วย settings ใหม่ (แทนที่ stopped instance) control เป็น **disabled ในขณะที่ instance active** — เฉพาะ stopped instance เท่านั้นที่สามารถ edit ได้

## Run from the code editor

Clicking **Run** ในตัวแก้ไขโค้ด เปิด dialog แทนที่จะยิง blind hard-coded run:

- **Trading account** (required) — cTrader account ที่ cBot เชื่อมต่อไป
- **Parameter set** (optional) — เลือก existing set หรือปล่อยให้ว่างเพื่อ run ด้วย **default parameter values** ของ cBot **+** button ข้าง selector สร้าง new parameter set inline (ดูด้านล่าง) และเลือกมัน
- **Symbol / Timeframe** default ถึง `EURUSD` / `h1` และสามารถเปลี่ยน; **Cancel** หรือ **Run**

บน **Run** editor saves + builds current source starts instance บน chosen account ด้วย chosen parameters แล้ว tails live container logs (log stream forwards signed-in user's auth cookie ไปยัง `/hubs/logs` SignalR hub เพื่อมันเชื่อมต่อแทนที่จะ fail ด้วย `Invalid negotiation response received`)

## Parameter sets

**parameter set** คือ named reusable set ของ cBot parameter overrides stored as flat JSON object mapping แต่ละ parameter name ถึง scalar value เช่น `{"Period": 14, "Label": "trend"}` ที่ run/backtest time มันเปลี่ยนเป็น cTrader `params.cbotset` file (`{ "Parameters": { … } }`) คุณสามารถ create/edit set as raw JSON จาก **Parameter sets** dialog ของ cBot หรือ inline จาก Run dialog

ทุก parameter set **belongs ถึง cBot**: New Parameter Set dialog แสดง cBots ทั้งหมดของคุณ และคุณ **must pick one** — creation block จนกว่า cBot จะเลือก set's **name is unique per cBot**: creating หรือ renaming set ถึง name ที่ set อื่น ของ cBot เดียวกันใช้แล้ว จะถูก reject (clear error ในการ dialog `409 Conflict` ที่ API) ชื่อเดียวกันอาจ reused บน **different** cBot

JSON เป็น **validated** on save: มันต้องเป็น single flat object ที่ values ทั้งหมดเป็น scalars (string / number / bool) non-object root array nested object `null` value หรือ malformed JSON จะถูก reject (clear error ในการ dialog `400 Bad Request` ที่ API) empty object `{}` อนุญาตและ means "no overrides"

## cTrader Console CLI notes

Backtests ต้อง `--data-mode` (default `m1`) dates as `dd/MM/yyyy HH:mm` และ `params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only) ดู `ContainerCommandHelpers`

## Nodes & scale

Execution capacity scale โดย adding node agents (self-register + heartbeat) ดู [node discovery](../operations/node-discovery.md) และ [scaling](../deployment/scaling.md)

## A trading account is required

Running หรือ backtesting cBot ต้องมี cTrader trading account เพื่อเชื่อมต่อ จนกว่าคุณจะ add one ภายใต้ **Trading accounts** **Run New cBot** / **Backtest New cBot** buttons ถูก disabled (พร้อมกับ tooltip) และหน้า แสดง prompt linking ไปยัง account setup — คุณไม่อีกต่อไปจะ hit raw `stream connect failed` error จาก bot ที่ไม่มี account
