---
description: "ส่งบัญชี cTrader หลักไปยังบัญชีเสมาหนึ่งบัญชีหรือมากกว่า — ข้ามโบรกเกอร์ ข้าม cID — พร้อมการควบคุมต่อตัวเลือก + การปรองดองเชื่อสัญญา"
---

# Copy trading

ส่งบัญชี cTrader **หลัก** ไปยังบัญชี **เสมา** หนึ่งหรือมากกว่า — ข้ามโบรกเกอร์ ข้าม cID — พร้อมการควบคุมต่อตัวเลือก + การปรองดองเชื่อสัญญา

## Concepts

- **Copy profile** — หนึ่งหลัก (`SourceAccountId`) + หนึ่ง + **destinations** วงจรชีวิต: `Draft → Running → Paused → Stopped` (`Error` ในกรณีล้มเหลว) รูทรวม: `CopyProfile` (เป็นเจ้าของ `CopyDestination`)
- **Destination** — หนึ่งบัญชีเสมา + ชุดกฎทั้งหมดว่าส่งหลักไปยังเสมาอย่างไร การกำหนดค่าทั้งหมดต่อตัวเลือก เพื่อให้หลักตัวเดียวป้อนเสมาอนุรักษ์นิยม + ก้าวร้าว พร้อมกัน
- **Copy engine host** — worker ที่ทำงานสำหรับโปรไฟล์ (`CopyEngineHost`) สมัครรับข้อมูลกระแสการนำเสนอหลัก ใช้แต่ละเหตุการณ์กับทุกตัวเลือก
- **Supervisor** — `CopyEngineSupervisor` บริการพื้นหลังบนแต่ละโหนด โฮสต์โปรไฟล์ที่กำหนด ซ่อมแซมตัวเองข้ามคลัสเตอร์ (ดู [scaling](../deployment/scaling.md))

## What gets mirrored

| Master event | Slave action |
|--------------|--------------|
| Market / market-range position open | Open a sized copy (labelled with the source position id) |
| Limit / stop / stop-limit pending order | Place the matching pending order |
| Pending order amend | Amend the mirrored pending order in place |
| Pending order cancel / expiry | Cancel the mirrored pending order |
| Partial close | Close the same proportion of the slave position |
| Scale-in (volume increase) | Open the added volume (opt-in) |
| Stop-loss / trailing-stop change | Amend the slave position's protection |
| Full close | Close the slave copy |

ทุกสำเนา **ติดป้ายกำกับด้วย source position/order id** หลังจากเชื่อมต่อใหม่ โฮสต์สร้างสถานะจากปรองดอง: เปิดสำเนาที่หลักถือแต่เสมาขาด ปิด orphans เสมาที่หลักไม่ถือแล้ว — **โดยไม่ทำสำเนาการค้า**

## Creating a profile

**นโยบายโปรไฟล์ใหม่** เปิด **หน้าเต็ม** แบบฟอร์ม (`/copy-trading/new`) ไม่ใช่กล่องโต้ตอบ — ชุดตัวเลือกใหญ่พอที่หน้าอ่านได้ดีกว่าบนโทรศัพท์และเดสก์ท็อป มันรวบรวมทุกอย่างล่วงหน้า: ชื่อโปรไฟล์ บัญชีต้นทาง (หลัก) บัญชีปลายทาง (เสมา) (การเลือกหลายรายการด้วยปุ่ม **เลือกทั้งหมด**; หลักที่เลือก ยกเว้นจากรายการเสมา) + ชุดตัวเลือกต่อปลายทางที่สมบูรณ์ **ตัวควบคุมทุกตัวมีเคล็ดลับช่วยเหลือ** อธิบายว่าทำอะไร และวิธีใช้งาน อินพุตที่มีโครงสร้างใช้ **ตัวควบคุมที่ตรวจสอบได้ถูกต้อง** — ตัวเลข/เปอร์เซ็นต์ผ่านเขตข้อมูลตัวเลข โหมด/ทิศทาง/ตัวกรองผ่านตัวเลือก ตัวกรองสัญลักษณ์ผ่านรายชิปสัญลักษณ์เพิ่ม/ลบ และแผนที่สัญลักษณ์ผ่านตาราง `Source → Destination (× multiplier)` เพิ่ม/ลบแถว — ไม่เคยเป็นข้อความที่คั่นด้วยเครื่องหมายจุลภาค ข้อมูลเข้าทั้งหมด **ตรวจสอบก่อนบันทึก** — ชื่อ/ต้นทาง/ปลายทางขาดหายไป พารามิเตอร์ขนาดที่ไม่เป็นบวก ขอบเขตลอตติดลบ/ไม่สอดคล้องกัน เปอร์เซ็นต์ drawdown นอกช่วง ไม่มีประเภทคำสั่งเปิดใช้งาน หรือตัวกรองสัญลักษณ์ว่างปรากฏเป็นรายการข้อผิดพลาด + บันทึกการแสดง ในการสร้าง โปรไฟล์จะถูกสร้าง + เสมาที่เลือกทั้งหมดเพิ่มด้วยการตั้งค่าที่เลือก จากนั้นหน้ากลับไปยังรายการ Copy Trading

**ส่งออก / นำเข้า** บล็อกการตั้งค่าเต็มหมด **ส่งออกเป็นไฟล์ JSON** และ **นำเข้า** ใหม่เพื่อเติมข้อมูลแบบฟอร์ม เพื่อให้การปรับแต่งสามารถนำกลับมาใช้ในโปรไฟล์โดยไม่ต้องพิมพ์ใหม่ แผนที่สัญลักษณ์สามารถในทำนองเดียวกัน **ส่งออก / นำเข้าเป็นไฟล์ CSV** (`Source,Destination,VolumeMultiplier`) — เตรียมแผนที่สัญลักษณ์โบรกเกอร์ขนาดใหญ่ในสเปรดชีตและโหลดในขั้นตอนเดียว ตัวควบคุมสัญลักษณ์เดียวกันและนำเข้า/ส่งออก CSV ยังพร้อมใช้งานในกล่องโต้ตอบปลายทางบนหน้า Copy Trading

การดำเนินการแถวให้สัมมติ วงจรชีวิต: **เริ่ม** เปิดใช้งานเฉพาะเมื่อไม่ทำงาน **หยุด** + **หยุดชั่วคราว** เฉพาะเมื่อทำงาน **ลบ** ปิดใช้งานในขณะทำงาน + ขอการยืนยันก่อนลบโปรไฟล์ + ปลายทาง

## Per-destination options

ตั้งค่าบนหน้า New Profile ในกล่องโต้ตอบปลายทางบนหน้า Copy Trading หรือผ่าน `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): fixed lot lot/notional multiplier proportional balance/equity/free-margin fixed risk % fixed leverage auto-proportional **risk-%-from-stop** (M7) บวก min/max lot bounds + force-min-lot **Risk-from-stop** ขนาดตัวเลือกเพื่อให้เสี่ยงเปอร์เซ็นต์ที่กำหนด *ของมันเอง* สมดุล ได้มาจาก **ระยะ stop-loss ของหลัก** (`master risks 2% → slave auto-risks 2%`): `lots = balance×% ÷ (stopDistance × contractSize)` หลักเปิด **โดยไม่** stop-loss ไม่มีระยะห่างในการขนาด → ใช้ **max-risk fallback lot** (M7) ที่กำหนดหากตั้งค่า อื่นไม่ข้าม (`no_stop_loss`) ไม่ถูกมัน สัดส่วน**equity**/**free-margin** ขนาดนอก **equity** บัญชีจริง (`balance + Σ floating P&L` ได้มาจาก cTrader Open API ซึ่งไม่ส่ง equity) ไม่ใช่สมดุลธรรมดา — ดังนั้นหลักนั่งบนกำไร/ขาดทุนที่เปิด ขนาดสำเนาทางด้านขวา นั่ง margin ไม่เปิดเผยโดย reconcile API ดังนั้น free-margin ถือเป็น equity (proxy available-funds ซื่อสัตย์); โหมดอื่นอ่านสมดุล + ข้ามรอบ revaluation เพิ่มเติม
- **Direction filter**: both / long-only / short-only **Reverse**: flip side (+ swap SL↔TP) สำหรับสำเนาผลตรงกันข้าม
- **Manage-only** (Ignore-New-Trades / Close-Only): mirror closes partial closes + protection changes บนตำแหน่งที่สำเนาแล้ว แต่เปิด **ไม่** ตำแหน่ง/คำสั่งที่ค้างอยู่เหล่านี้ (ข้าม `manage_only`) ใช้เพื่อลดตัวเลือกลงโดยไม่ตัดสำเนาที่มีอยู่
- **Sync-Open-on-start** / **Sync-Closed-on-start** (ค่าเริ่มต้นเปิด): บน **resync แรก** ของโปรไฟล์ ไม่ว่าจะเปิดสำเนาสำหรับตำแหน่งที่มีอยู่ก่อนหลัก + ไม่ว่าจะปิดสำเนาหลักปิดขณะโปรไฟล์หยุด ทั้งสองใช้เฉพาะที่จุดเริ่มต้น — การเชื่อมต่อใหม่กลางการทำงานอย่างไรก็ตาม ปรองดองทั้งหมดเพื่อให้การปรองดองการฟื้นตัว
- **Symbol map** + **symbol filter** (whitelist / blacklist) รายการแผนที่สัญลักษณ์แต่ละรายการมี **optional per-symbol volume multiplier** (cMAM per-symbol override) ขนาดสำเนากะของสัญลักษณ์นั้นบนตัวเลือกของ destination (1 = ไม่มีการเปลี่ยนแปลง) แผนที่ทั้งหมดนำเข้า/ส่งออกเป็น **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; คอลัมน์ `Source,Destination,VolumeMultiplier`) — แต่ละแถวตรวจสอบผ่านวัตถุค่าโดเมน ดังนั้นไฟล์ที่มีรูปแบบไม่ถูกต้องไม่สามารถสร้างแผนที่ที่ไม่ถูกต้องได้
- **Trading-hours window** (C18) — ต่อปลายทาง วันละยูทีซี วินโดว์ (`start`/`end` minutes-of-day, end exclusive; `start == end` = all-day) เปิดใหม่นอกวินโดว์ข้าม (`trading_hours`); วินโดว์ที่มี `start > end` ห่อหลังเที่ยงคืน (เช่น 22:00–06:00) ตำแหน่งที่มีอยู่ยังคงมีการจัดการ
- **Source-label filter** (C18 cTrader เทียบเท่าของตัวกรองเลขมหัศจรรย์ MT) — เมื่อตั้งค่า สำเนาเฉพาะการค้าหลักที่มีป้ายกำกับตรงตามข้อกำหนด (เช่น บทบาทหนึ่งจำนวนการค้า หรือป้ายกำกับด้วยตนเอง); อื่นข้าม (`source_label`) ว่าง = สำเนาทั้งหมด ดำเนินการต่อ `ExecutionEvent.SourceLabel` จากตำแหน่ง/คำสั่งหลัก `TradeData.Label` เคารพบน resync เกินไป
- **Account protection** (ZuluGuard / Global Account Protection) — ดู **live equity** ปลายทาง (`balance + Σ floating P&L` โพล ทุก `CopyDefaults.EquityGuardInterval`) สำหรับ `StopEquity` ระดับพื้น และ/หรือ `TakeEquity` เพดานแบบเลือก ใช้โหมด: **CloseOnly** (หยุดสำเนาใหม่ เก็บการจัดการที่มีอยู่) **Frozen** (หยุดเปิด) **SellOut** (ปิด **ทุก** สำเนาปลายทาง ทันที) เมื่อยิง จองเสมา — ไม่มีการเปิดใหม่จนกว่าโฮสต์จะเริ่มต้นใหม่ — + `CopyAccountProtectionTriggered` alert เพิ่ม `SellOut` ต้องการ `StopEquity`; `TakeEquity` ต้องนั่งเหนือ `StopEquity` **ไม่มีประกันสำคัญ:** sell-out ใช้การนำเสนอตลาด — เช่นเดียวกับการ equivalent ของคู่แข่ง ไม่สามารถรับประกันราคาเติมเต็มได้ในตลาดที่รวดเร็ว/gapped
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` ปิด **ทุก** สำเนาตำแหน่ง ทันที บนทุกปลายทาง + ล็อคกับการเปิดใหม่ ส่งต่อข้ามกระบวนการ: API ตั้งค่าแฟล็ก supervisor ส่งให้โฮสต์ที่ทำงาน (ใช้ช่องทางการหมุนโทเค็นใหม่) ซึ่งปรับเสียงโดยสถานที่; แฟล็กล้าง ดังนั้นยิงอย่างแน่นอน (`CopyFlattenAll` alert) ผู้ใช้จึงหยุด/หยุดโปรไฟล์
- **Prop-firm rule guard** (C7) — enforcement prop-firm copier ผู้ใช้ขอ ต่อปลายทาง **daily-loss cap** (ขาดทุนจากวันที่เปิด equity) และ/หรือ **trailing-drawdown** limit (ขาดทุนจากยอดวิ่ง equity) ทั้งในสกุลเงินฝากเงิน ใช้ breach destination **auto-flattened** (ทุกสำเนาปิด) + **ล็อคออก** นอก UTC วันที่เหลือ (เปิดใหม่ข้าม `prop_lockout`); `CopyPropRuleBreached` alert ยิง Lockout ล้างเมื่อ UTC วันม้วน (fresh baseline/peak taken) ใช้โพลจังหวะเดียวกับการป้องกันบัญชี
- **Execution jitter** (C11 ปิดตามค่าเริ่มต้น) — ล่าช้า `0..N` ms สุ่มก่อนวางแต่ละสำเนา เพื่อ de-correlate ประทับเวลาคำสั่งที่เกือบจะเหมือนกันทั่ว **ผู้ใช้ของตัวเอง** บัญชี **Compliance caveat:** ช่วยเหลือสำหรับ บริษัท prop ที่ *อนุญาต* การสำเนา — **ไม่** เครื่องมือในการหลีกเลี่ยงบริษัทที่ห้าม; อยู่ในกฎของบริษัทคุณคือความรับผิดชอบของคุณ
- **Config lock** (C9) — การตั้งค่าปลายทางด้าน freeze สำหรับช่วงเวลา (`POST …/destinations/{id}/lock` ด้วยนาที) ในขณะที่ล็อก ปลายทางไม่สามารถลบได้ (aggregate เห็นปฏิเสธด้วย `CopyDestinationConfigLocked`) — guard ตั้งใจต้านแนวโน้มหลายเท่าเสียใจในช่วง drawdown ระยะเวลาการล็อคหมดอายุโดยอัตโนมัติ
- **Consistency pre-alert** (C10) — เตือน (หนึ่งครั้งต่อวัน UTC) เมื่อ **ส่วนลาภของวันปลายทาง** ถึงเปอร์เซ็นต์ที่กำหนดของวันที่เปิด equity (`CopyConsistencyThresholdApproaching`) เพื่อให้กฎความสอดคล้องกัน prop-firm เคารพ *ก่อน* มันจำนวน ด้านกำไรอิสระของด้านขาดทุน; วิ่งปิด baseline วันเดียวกับ prop-rule guard
- **Order-type filter** — เลือกว่ากำลังคำสั่งใด ของหลัก เพื่อ copy: market market-range limit stop stop-limit (`CopyOrderTypes` flags; default all) cMAM-style selectivity
- **Copy SL / Copy TP** — mirror master's stop-loss / take-profit หรือจัดการการป้องกันอย่างอิสระ
- **Copy trailing stop** **mirror partial close** **mirror scale-in** — แต่ละ independently toggleable
- **Copy pending expiry** (default on) — mirror master pending order's Good-Till-Date expiry timestamp
- **Copy master slippage** (default on) — for market-range + stop-limit orders place slave order with master's exact slippage-in-points (base price taken from slave's live spot)
- **Guards**: max drawdown % daily loss cap max copy delay slippage filter (skip copy if slave price moved beyond N pips from master entry) **Max copy delay** measured against master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) via injected `TimeProvider`: signal older than configured max-lag skipped so stale copy never placed late (previously delay always zero + guard dead)
- **SL/TP precision normalization** (M6) — copied stop-loss/take-profit prices rounded to **destination** symbol's digit precision before amend so master price at finer precision (or cross-broker digit mismatch) never trips server's `INVALID_STOPLOSS_TAKEPROFIT`
- **Rejection circuit breaker / Follower Guard** (G8) — destination rejecting `CopyDefaults.RejectionBudget` opens in a row is **tripped**: no new opens for cooldown window (`CopyDestinationTripped` alert fires) stopping rejection storm from hammering (prop-firm) account. Existing positions still managed + closed while tripped; breaker auto-resets after cooldown + successful copy clears counter.
- **Lot sanity ceiling** (C14) — absolute max copy size and/or multiple-of-master cap. Computed copy exceeding absolute cap or exceeding `N×` master's own lot size **hard-blocked** (surfaced as `lot_sanity` skip counted on `cmind.copy.skipped`) not placed — defends against catastrophic-oversize class (0.23-lot master turning into 3 lots on each receiver via runaway multiplier or rounding bug). Both dimensions default `0` (off).

## Reliability & edge cases

Engine สร้างสำหรับความจริงที่อะไรก็ได้ล้มเหลวได้ตลอดเวลา:

- **Slave-pending fill-correlation timeout** (C13) — สำเนา slave pending ที่มีหลัก pending หายไป (ไม่นอน หรือ freshly filled) ยกเลิกหลังจาก correlation timeout ดังนั้นสำเนา slave ไม่สามารถเติมเต็ม uncorrelated เข้า unmanaged position (`CopyPendingTimedOut`) Resync ยังเก็บความสะอาด order-id-labelled filled-pending orphan
- **Robust close/flatten** (M8) — ปิด orphan บน resync หรือการปรับเสียง على guard breach ความอดทนตำแหน่ง broker ปิดแล้ว (`POSITION_NOT_FOUND`): ปิดแต่ละตำแหน่งทำงานอย่างอิสระ ดังนั้นไม่มี id เก่าเลย aborts resync หรือปล่อยให้ส่วนที่เหลือของบัญชี un-flattened

- **Start with master already in trades** — บน start host reconciles + opens copies for master's existing positions
- **Connection drops / desync** — on reconnect host reconciles: opens missing copies closes orphans re-labels pendings No duplicate orders
- **Order placement failure** — failure on one destination logged never blocks other destinations
- **Single valid token per cID** — cTrader invalidates cID's old access token moment new one issued cMind swaps running host's token **in place** (re-auth on live socket) so copying continues without dropping stream See [token lifecycle](token-lifecycle.md)

## Auditability

ทุกการดำเนิน emits structured source-generated log event (`LogMessages`) with profile id destination cID order/position ids + values — order placed/skipped (with reason) partial close protection applied trailing applied pending placed/amended/cancelled expiry mirrored market-range slippage mirrored token swapped resync summary. นี่คือเส้นทางตรวจสอบสำหรับ compliance + dispute resolution

Alongside logs engine emits **OpenTelemetry metrics** on `cMind.Copy` meter (registered in shared OTel pipeline exported over OTLP / to Azure Monitor like rest): `cmind.copy.latency` (master-event → dispatch ms) `cmind.copy.dispatch.duration` (fan-out to all destinations ms) `cmind.copy.slippage.points` `cmind.copy.placed` (tagged by destination) `cmind.copy.skipped` (tagged by reason) + `cmind.copy.failed`. สิ่งเหล่านี้ทำให้ latency/slippage regression measurable ไม่ใช่เพียงมองเห็นในบรรทัด log — live suite asserts them against budget

## API

- `GET /api/copy/profiles` — list
- `POST /api/copy/profiles` — create (with optional destination account ids)
- `GET /api/copy/profiles/{id}` — full detail incl. every destination option
- `POST /api/copy/profiles/{id}/destinations` — add a destination with the full option set
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remove
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes decision filters order-type filter expiry copy market-range/stop-limit slippage SL/TP toggles partial close pending amend/cancel start-with-open disconnect→desync→resync in-place token swap cross-cID invalidation. Runs against `FakeTradingSession` cTrader-faithful in-memory simulator
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim token-version propagation on real Postgres
- **E2E** (`tests/E2ETests`) — destination-option round-trip through API + UI full lifecycle
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap order rejection market-range rejection token rotation node death) drive `CopyEngineHost` to quiescence + assert convergence invariants. See [testing/stress-testing.md](../testing/stress-testing.md). This suite surfaced + fixed real startup race: `OnReconnected` wired before initial reference-load + resync so socket flap during startup could run second resync concurrently + corrupt host's non-concurrent state dictionaries — startup load + first resync now run under `_stateGate`
- **Live** — real cTrader demo accounts; see [testing/live-copy-trading.md](../testing/live-copy-trading.md)

See [dev-credentials.md](../testing/dev-credentials.md) for single credentials file live + E2E tiers read.

## Profile controls and destination management

Start/stop are icon buttons on each profile row (disabled when the action does not apply). Source and
destination accounts are shown by their **account number**, never an internal id. Clicking a profile
opens a **dialog** to manage its destination accounts (add/remove with full per-destination settings).
