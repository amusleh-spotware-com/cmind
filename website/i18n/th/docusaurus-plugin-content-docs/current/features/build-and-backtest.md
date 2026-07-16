---
description: "สร้าง, รัน, backtest cTrader cBots (C# และ Python ทั้งคู่ที่ .NET) จากเบราว์เซอร์ Monaco IDE, รัน official ghcr.io/spotware/ctrader-console image"
---

# Build & backtest cBots

สร้าง, รัน, backtest cTrader cBots (C# **และ** Python ทั้งคู่ที่ .NET) จากเบราว์เซอร์ Monaco IDE, รัน official `ghcr.io/spotware/ctrader-console` image

## Build

- **Builder** page โฮสต์ Monaco editor; `CBotBuilder` compile project ด้วย `dotnet build` **ในคอนเทนเนอร์ที่ทำลายแล้ว** (`AppOptions.BuildImage`, work dir bind-mount ที่ `/work`) เพื่อให้ MSBuild เป้าหมายที่ไม่น่าเชื่อถือของผู้ใช้ไม่สามารถเข้าถึงโฮสต์ได้ NuGet restore ถูกแคชระหว่าง builds ผ่าน shared volume Web host ต้องเข้าถึง Docker socket
- C# + Python starter templates อยู่ใน `src/Nodes/Builder/Templates/`

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`) Transition แทนที่เอนทิตี (id change), container id ถูกพกพาไป
- `NodeScheduler` เลือกโหนดที่มีภาระน้อยที่สุดที่มีคุณสมบัติเพียงพอ; `ContainerDispatcherFactory` เส้นทางไปยัง remote node HTTP agent หรือ local Docker dispatcher
- Completion pollers reconcile exited containers (backtest containers self-exit ผ่าน `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed
- Live container logs stream ไปยังเบราว์เซอร์ผ่าน SignalR; backtest equity curves parsed จาก report + charted

## Backtest market data is cached per account

cTrader Console ดาวน์โหลดข้อมูลประวัติ tick/bar เข้าไปใน `--data-dir` ของมัน ไดเรกทอรีนั้นเป็น **stable, persistent cache keyed on the trading account** (account number ของมัน) — bind-mounted จากดิสก์ของโหนดที่เส้นทางคอนเทนเนอร์ของมันเอง (`/mnt/data`), a **separate, non-nested mount** จากต่อ-instance work dir ดังนั้นทุก backtest บน account เดียวกัน **reuses** ข้อมูลที่ดาวน์โหลดแล้วแทนที่จะ re-download ที่แต่ละครั้ง (ก่อนหน้านี้ data dir อยู่ภายใต้ต่อ-instance work dir ซึ่ง id เปลี่ยนทุกครั้ง ซึ่งบังคับให้ fresh download ทุก backtest) Ephemeral per-instance work dir ยังคงเก็บ algo, params, password และ report; shared data cache นับรวมในการใช้งาน backtest-data ของโหนด และ cleared โดย node-clean action

## Backtest settings

**Backtest** dialog exposes ทุกการตั้งค่าที่ cTrader Console backtest CLI ยอมรับ เพื่อให้คุณไม่ต้องสัมผัสบรรทัดคำสั่ง:

- **From / To** — backtest window (`--start` / `--end`)
- **Data mode** — หนึ่งในสามโหมด cTrader (`--data-mode`): **Tick data** (`tick`, accurate), **m1 bars** (`m1`, fast), หรือ **Open prices only** (`open`, fastest)
- **Starting balance** — defaults ไป `10000` (`--balance`) A **0 balance places no trades และ makes cTrader emit an empty report ที่ crashed on** ("Message expected") ดังนั้น non-zero balance จึงถูกส่งเสมอ
- **Commission** และ **Spread** — `--commission` / `--spread` (spread ในหน่วย pips)
- **Data file** (optional) — node-side path ไป historical data file (`--data-file`); ปล่อยไว้ว่างเพื่อใช้ downloaded/cached data
- **Expose environment variables** — toggle ที่ผ่าน host environment variables ไปยัง cBot (the `--environment-variables` flag)

## Instance detail page

การเปิด instance (`/instance/{id}`) แสดง live status, logs และ — สำหรับ backtest — equity curve **browser tab title** สะท้อนถึง instance ที่เฉพาะเจาะจง (**cBot name · kind · symbol**, e.g. `TrendBot · Backtest · EURUSD`) ดังนั้นแท็บ live-run และแท็บ backtest จึงแตกต่างกันได้ตัดสิน A run และ backtest ของ cBot เดียวกัน tracked เป็น **lineages** (stable lineage id ที่พกพาข้ามการเปลี่ยนผ่าน state) ดังนั้นเพจจึงติดตาม instance เดียวตัวเดียว และไม่เคยผสม run's data ด้วย backtest's

## Instance lifecycle controls

แต่ละ instance row (และ detail page ของมัน) มี state-correct controls **active** instance แสดง **Stop**; a **terminal** one (Stopped / Completed / Failed) แสดง **Start (▶)** ไปเพื่อ re-launch มันด้วย cBot เดียวกัน, account, symbol, timeframe, parameter set และ image (a run restarts เป็น run, a backtest เป็น backtest) การคลิก Stop แสดง "Stopping…" notice และ disables icon จนกว่าจะ resolves และ newly created run ปรากฏในรายการทันที — no page reload

Console logs ถูก **persisted เมื่อ instance terminates** — สำหรับ run (on Stop) และสำหรับ **backtest** (on completion) เหมือนกัน — ดังนั้นบันทึก last run จึงยังคงสามารถดูได้บนหน้ารายละเอียด และผ่าน log toolbar, **copied ไปยัง clipboard** (Copy logs icon) หรือ **downloaded** (Download logs icon) แม้หลัง container หายไป ทั้งสองทำงานบน full console log ของ instance, ไม่ใช่แค่ on-screen tail

An **uploaded** `.algo` ไม่เคยถูกสร้างที่นี่ ดังนั้น **Last Build** column ของมันบน cBots page ถูกปล่อยไว้ว่างเปล่า (มันแสดงเวลา build เฉพาะสำหรับ cBots ที่คุณสร้างในเบราว์เซอร์)

## Edit & re-run a stopped instance

A **stopped** instance (run หรือ backtest) มี **Edit** control — an icon บนแถว list ของมัน **และ** beside Start/Stop บน detail page ของมัน — ที่เปิด dialog **prefilled** ด้วยการกำหนดค่าปัจจุบันของมัน คุณสามารถเปลี่ยน **trading account, symbol, timeframe, parameter set และ image tag** (และสำหรับ backtest, the **window และทั้งหมด backtest settings** ด้านบน) แล้ว **Save & start** re-launches มันด้วยการตั้งค่าใหม่ (แทนที่ stopped instance) Control ถูก **disabled ขณะที่ instance ใช้งาน** — เฉพาะ stopped instance ที่สามารถแก้ไขได้

## Run from the code editor

การคลิก **Run** ในเอดิเตอร์โค้ดจะเปิด dialog แทนการยิง blind, hard-coded run:

- **Trading account** (required) — cTrader account ที่ cBot เชื่อมต่อไป
- **Parameter set** (optional) — เลือกชุดที่มีอยู่ หรือปล่อยไว้ว่างเพื่อรันด้วย cBot's **default parameter values** **+** button ข้าง selector สร้าง new parameter set inline (see below) และเลือกมัน
- **Symbol / Timeframe** default ไป `EURUSD` / `h1` และสามารถเปลี่ยนได้; **Cancel** หรือ **Run**

บน **Run** editor saves + builds current source, starts instance บน account ที่เลือก ด้วย parameters ที่เลือก จากนั้น tails live container logs (log stream ส่งต่อ signed-in user's auth cookie ไปยัง `/hubs/logs` SignalR hub เพื่อให้มันเชื่อมต่อแทนที่จะล้มเหลวด้วย `Invalid negotiation response received`)

## Parameter sets

A **parameter set** เป็น named, reusable set ของ cBot parameter overrides ที่เก็บไว้เป็น flat JSON object mapping parameter name แต่ละตัว ไปยัง scalar value เช่น `{"Period": 14, "Label": "trend"}` ที่เวลา run/backtest มันถูกเปลี่ยนเป็น cTrader `params.cbotset` file (`{ "Parameters": { … } }`) คุณสามารถสร้าง/แก้ไขชุดเป็น raw JSON จาก cBot's **Parameter sets** dialog หรือ inline จาก Run dialog

ทุก parameter set **belongs ไปยัง cBot**: New Parameter Set dialog ระบุทั้งหมด cBots ของคุณ และคุณ **must pick one** — creation ถูกบล็อกจนกว่า cBot จะถูกเลือก Set's **name is unique per cBot**: creating หรือ renaming set ไปยังชื่อที่ set อื่นของ cBot เดียวกันใช้แล้ว is rejected (clear error ในรายการ dialog, `409 Conflict` ที่ API) ชื่อเดียวกันอาจถูกนำกลับมาใช้บน **different** cBot

JSON ถูก **validated** บน save: มันต้องเป็น single flat object ที่ values ทั้งหมด scalar (string / number / bool) A non-object root, an array, nested object, a `null` value, หรือ malformed JSON is rejected (clear error ในรายการ dialog, `400 Bad Request` ที่ API) Empty object `{}` ถูกอนุญาต และหมายความว่า "no overrides"

## cTrader Console CLI notes

Backtests ต้อง `--data-mode` (default `m1`) dates เป็น `dd/MM/yyyy HH:mm` และ `params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only) See `ContainerCommandHelpers`

## Nodes & scale

Execution capacity scale ด้วย adding node agents (self-register + heartbeat) See [node discovery](../operations/node-discovery.md) และ [scaling](../deployment/scaling.md)

## A trading account is required

การรัน หรือ backtest cBot ต้อง cTrader trading account เพื่อเชื่อมต่อไป จนกว่าคุณจะเพิ่มหนึ่งรายการภายใต้ **Trading accounts**, the **Run New cBot** / **Backtest New cBot** buttons ถูก disabled (ด้วย tooltip) และหน้า shows prompt linking ไปยัง account setup — คุณไม่อีกต่อไป hit raw `stream connect failed` error จาก bot ที่ไม่มี account
