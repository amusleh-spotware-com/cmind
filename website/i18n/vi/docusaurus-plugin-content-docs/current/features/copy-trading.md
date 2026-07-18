---
description: "Sao chép tài khoản master cTrader sang một hoặc nhiều tài khoản slave — xuyên broker, xuyên cID — với kiểm soát từng đích đến + hòa giải cấp độ tiền tệ."
---

# Copy trading

Sao chép **master** cTrader tài khoản sang một hoặc nhiều tài khoản **slave** — xuyên broker, xuyên cID — với kiểm soát từng đích đến + hòa giải cấp độ tiền tệ.

## Concepts

- **Copy profile** — một master (`SourceAccountId`) + một hoặc nhiều **destinations**. Vòng đời: `Draft → Running → Paused → Stopped` (`Error` khi thất bại). Aggregate root: `CopyProfile` (sở hữu `CopyDestination`).
- **Destination** — một tài khoản slave + bộ quy tắc đầy đủ cho cách thức master được sao chép vào nó. Tất cả cấu hình từng đích đến, vì vậy một master có thể cấp cho cả tài khoản slave bảo thủ và tích cực cùng một lúc.
- **Copy engine host** — worker đang chạy cho profile (`CopyEngineHost`). Đăng ký luồng thực thi master, áp dụng mỗi sự kiện cho mỗi đích đến.
- **Supervisor** — `CopyEngineSupervisor`, dịch vụ nền trên mỗi nút. Lưu trữ các hồ sơ được gán, tự chữa lành trên toàn cụm (xem [scaling](../deployment/scaling.md)).

## What gets mirrored

| Master event | Slave action |
|--------------|--------------|
| Market / market-range position open | Open a sized copy (labelled with the source position id) |
| Limit / stop / stop-limit pending order | Place the matching pending order, carrying the master's stop-loss / take-profit |
| Pending order amend | Amend the mirrored pending order in place (including its stop-loss / take-profit) |
| Pending order cancel / expiry | Cancel the mirrored pending order |
| Partial close | Close the same proportion of the slave position |
| Scale-in (volume increase) | Open the added volume (opt-in) |
| Stop-loss / trailing-stop change | Amend the slave position's protection |
| Full close | Close the slave copy |

Mỗi bản sao được **gắn nhãn bằng id vị trí/đơn hàng nguồn**. Sau khi kết nối lại, host xây dựng lại trạng thái từ hòa giải: mở các bản sao mà master nắm giữ nhưng slave bị mất, đóng "mồ côi" slave mà master không còn nắm giữ — **mà không trùng lặp giao dịch**.

## Creating a profile

**Hồ sơ mới** mở một biểu mẫu **toàn trang** chuyên dụng (`/copy-trading/new`), không phải hộp thoại — bộ tùy chọn đủ lớn để một trang đọc tốt hơn trên điện thoại và máy tính để bàn. Nó thu thập mọi thứ phía trước: tên hồ sơ, tài khoản nguồn (master), tài khoản đích đến (slave) (lựa chọn đa với nút **Chọn tất cả**; master được chọn được loại khỏi danh sách slave), + bộ tùy chọn đích đến đầy đủ. **Mỗi kiểm soát đều mang theo một mẹo trợ giúp** giải thích nó làm gì và cách sử dụng. Các đầu vào có cấu trúc sử dụng **các kiểm soát được xác thực đúng cách** — số/phần trăm thông qua các trường số, chế độ/hướng/bộ lọc thông qua các trình chọn, bộ lọc ký hiệu thông qua danh sách thêm/xóa chip ký hiệu, và bản đồ ký hiệu thông qua bảng thêm/xóa hàng `Nguồn → Đích (× hệ số)` — không bao giờ là blob văn bản phân cách bằng dấu phẩy. Tất cả các đầu vào được **xác thực trước khi lưu** — tên/nguồn/đích đến bị mất, tham số kích thước không dương, giới hạn lot âm/không nhất quán, phần trăm drawdown ngoài phạm vi, không có loại lệnh được bật, hoặc bộ lọc ký hiệu trống sẽ xuất hiện dưới dạng danh sách lỗi + chặn lưu. Khi tạo, hồ sơ được tạo + mỗi slave được chọn được thêm vào với các cài đặt được chọn, sau đó trang quay trở lại danh sách Copy Trading.

**Xuất / Nhập.** Toàn bộ khối cài đặt có thể được **xuất vào tệp JSON** và **nhập lại** để điền trước biểu mẫu, vì vậy một điều chỉnh có thể được sử dụng lại trên các hồ sơ mà không cần gõ lại. Bản đồ ký hiệu cũng có thể **xuất / nhập dưới dạng tệp CSV** (`Nguồn,Đích,HệSốThểTích`) — chuẩn bị bản đồ ký hiệu broker lớn trong bảng tính và tải nó trong một bước. Các kiểm soát ký hiệu và xuất/nhập CSV tương tự cũng có sẵn trong hộp thoại đích đến trên trang Copy Trading.

Hành động hàng tôn trọng vòng đời: **Bắt đầu** chỉ được bật khi không chạy, **Dừng** + **Tạm dừng** chỉ khi chạy, **Xóa** bị vô hiệu hóa khi đang chạy + yêu cầu xác nhận trước khi xóa hồ sơ + đích đến.

Một hồ sơ vừa được bắt đầu sẽ hiển thị ngắn gọn trạng thái **Starting** (không phải green *Running*) trong khi host của nó tải dữ liệu tham chiếu và chạy resync đầu tiên — nó chưa sao chép đơn hàng trên các đích đến. Nó chuyển sang **Running** khi resync đầu tiên hoàn thành và engine có thể sao chép. Starting được coi là running cho các điều khiển hàng (Start disabled, Stop và live-logs enabled, Edit/Delete blocked), vì vậy một hồ sơ warming không thể được khởi động lại hoặc chỉnh sửa mid-startup. Giai đoạn warm-up được theo dõi trong quá trình trên nút lưu trữ hồ sơ; một hồ sơ được lưu trữ trên replica khác (hoặc một hồ sơ không thể được lưu trữ — tài khoản nguồn/đích đến không được liên kết qua Open API) hiển thị trạng thái thuần túy của nó.

## Per-destination options

Đặt trên trang New Profile, trong hộp thoại đích đến trên trang Copy Trading, hoặc qua `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): lot cố định, lot/multiplier danh nghĩa, proportional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional, **risk-%-from-stop** (M7). Cộng với min/max lot bounds + force-min-lot. **Risk-from-stop** cấp phát đích đến để nó rủi ro cấp phát phần trăm *của riêng nó* balance, được lấy từ **khoảng cách dừa lỗi của master** (`master risks 2% → slave auto-risks 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master mở **không có** stop-loss không có khoảng cách để cấp phát so với → sử dụng **max-risk fallback lot** được cấu hình (M7) nếu được đặt, nếu không bị bỏ qua (`no_stop_loss`) không được đoán. Proportional-**equity**/**free-margin** cấp phát ngoài **equity** tài khoản thực (`balance + Σ floating P&L`, lấy từ cTrader Open API không cung cấp equity), không phải balance thuần — vì vậy master ngồi trên lợi nhuận/lỗi mở kích thước bản sao đúng. Lề được sử dụng không được hiển thị bởi API hòa giải, vì vậy free-margin được coi là equity (proxy tiền tệ có sẵn thực tế); các chế độ khác đọc balance + bỏ qua vòng đánh giá lại round-trip bổ sung.
- **Direction filter**: both / long-only / short-only. **Reverse**: flip side (+ swap SL↔TP) cho contrarian copy.
- **Manage-only** (Ignore-New-Trades / Close-Only): mirror closes, partial closes + protection changes trên các vị trí đã được sao chép, nhưng mở **không có** vị trí/lệnh đang chờ xử lý mới (bỏ qua `manage_only`). Sử dụng để cuộn đích đến mà không cắt bản sao hiện có.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default on): trên **lần đầu tiên** resync của hồ sơ, có mở bản sao cho các vị trí đã tồn tại trước đó của master hay không, + có đóng các bản sao mà master đã đóng trong khi hồ sơ dừng lại hay không. Cả hai chỉ áp dụng khi bắt đầu — reconnect mid-run luôn hòa giải đầy đủ vì vậy desync khôi phục bất kể.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Mỗi mục bản đồ ký hiệu mang theo **multiplier khối lượng từng ký hiệu** (cMAM override từng ký hiệu) scaling copy size cho ký hiệu đó trên đích đến cấp phát từng đích đến (1 = không thay đổi). Toàn bộ bản đồ nhập/xuất dưới dạng **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; columns `Source,Destination,VolumeMultiplier`) — mỗi hàng được xác thực thông qua domain value objects, vì vậy tệp không đúng định dạng không thể tạo bản đồ không hợp lệ.
- **Trading-hours window** (C18) — cửa sổ UTC hàng ngày từng đích đến (`start`/`end` phút trong ngày, end exclusive; `start == end` = all-day). New opens ngoài cửa sổ bỏ qua (`trading_hours`); cửa sổ với `start > end` quấn sau nửa đêm (ví dụ: 22:00–06:00). Vị trí hiện có vẫn được quản lý.
- **Source-label filter** (C18, cTrader equivalent of MT magic-number filter) — khi được đặt, chỉ sao chép các giao dịch master có nhãn **chính xác khớp** (ví dụ: giao dịch của một bot, hoặc nhãn chỉ thủ công); nếu không bỏ qua (`source_label`). Empty = sao chép tất cả. Mang theo `ExecutionEvent.SourceLabel` từ vị trí/đơn hàng của master `TradeData.Label`, tôn trọng trên resync quá.
- **Account protection** (ZuluGuard / Global Account Protection) — xem **live equity** đích đến (`balance + Σ floating P&L`, polled mỗi `CopyDefaults.EquityGuardInterval`) so với sàn `StopEquity` và/hoặc trần `TakeEquity` tùy chọn. Khi vi phạm, áp dụng chế độ: **CloseOnly** (dừng bản sao mới, giữ quản lý hiện có), **Frozen** (dừng mở), **SellOut** (đóng **mỗi** bản sao trên đích đến ngay lập tức). Khi được kích hoạt, đích đến bị khóa — không có mở mới cho đến khi host khởi động lại — + cảnh báo `CopyAccountProtectionTriggered` được nâng cao. `SellOut` yêu cầu `StopEquity`; `TakeEquity` phải nằm trên `StopEquity`. **No-guarantee caveat:** sell-out sử dụng thực thi thị trường — giống như của mỗi đối thủ cạnh tranh, không thể đảm bảo giá điền trong thị trường nhanh/gapped.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` ngay lập tức đóng **mỗi** vị trí được sao chép trên mỗi đích đến + khóa chống mở mới. Định tuyến qua quy trình: API đặt cờ, giám sát viên cung cấp cho host chạy (sử dụng lại kênh xoay token), bao bì phẳng tại chỗ; cờ được xóa để kích hoạt chính xác một lần (`CopyFlattenAll` alert). Người dùng sau đó tạm dừng/dừng hồ sơ.
- **Prop-firm rule guard** (C7) — prop-firm copier enforcement users yêu cầu. Per destination, **daily-loss cap** (loss from day's opening equity) và/hoặc **trailing-drawdown** limit (loss from running peak equity), cả hai trong tiền gửi tiền tệ. Khi vi phạm đích đến **auto-flattened** (mỗi bản sao đóng) + **khóa** phần còn lại của ngày UTC (mở mới bỏ qua `prop_lockout`); `CopyPropRuleBreached` alert kích hoạt. Khóa xóa khi ngày UTC cuộn (baseline/peak mới lấy). Chia sẻ cùng live-equity poll như account protection.
- **Execution jitter** (C11, off by default) — random `0..N` ms delay trước khi đặt mỗi bản sao, để de-correlate gần giống hệt nhau order timestamps trên tài khoản **của chính mình** của người dùng. **Compliance caveat:** hỗ trợ cho các công ty nhà đất *cho phép* sao chép — **không phải** công cụ để tránh công ty that forbids it; ở lại trong quy tắc của công ty bạn là trách nhiệm của bạn.
- **Config lock** (C9) — freeze destination's settings cho khoảng thời gian (`POST …/destinations/{id}/lock` với phút). Trong khi bị khóa, đích đến không thể bị xóa (aggregate reject với `CopyDestinationConfigLocked`) — guard có ý chống lại những thay đổi thoáng qua trong lúc drawdown. Khóa hết hạn tự động vào dấu thời gian của nó.
- **Consistency pre-alert** (C10) — cảnh báo (một lần trên mỗi ngày UTC) khi **lợi nhuận hàng ngày** đích đến đạt đến phần trăm được cấu hình của equity opening day (`CopyConsistencyThresholdApproaching`), vì vậy prop-firm consistency rule respected *trước* nó trips. Profit-side, độc lập với loss-side lockout; chạy ngoài cùng day baseline như prop-rule guard.
- **Order-type filter** — chọn chính xác loại đơn hàng master nào để sao chép: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` flags; default all). cMAM-style selectivity.
- **Copy SL / Copy TP** — mirror master's stop-loss / take-profit, hoặc manage protection một cách độc lập. Áp dụng cho **cả** vị trí mở **và** lệnh đang chờ xử lý resting — một bản sao limit/stop/stop-limit được đặt và sửa đổi với SL/TP của master order (hoán đổi dưới **Reverse**), vì vậy bảo vệ được đính kèm từ thời điểm order đang chờ fills, không chỉ sau.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — mỗi cái độc lập toggleable.
- **Copy pending expiry** (default on) — mirror master pending order's Good-Till-Date expiry timestamp.
- **Copy master slippage** (default on) — for market-range + stop-limit orders, place slave order với master's exact slippage-in-points (base price lấy từ slave's live spot).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (skip copy nếu slave price di chuyển ngoài N pips từ master entry). **Max copy delay** đo lường so với master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) qua injected `TimeProvider`: signal cũ hơn configured max-lag bỏ qua, vì vậy stale copy không bao giờ được đặt muộn (trước đây delay luôn bằng không + guard chết).
- **SL/TP precision normalization** (M6) — copied stop-loss/take-profit prices làm tròn đến **destination** symbol's digit precision trước khi amend (on positions **and** pending-order placement/amend), vì vậy master price ở fine precision (hoặc cross-broker digit mismatch) không bao giờ trips server's `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — đích đến rejecting `CopyDefaults.RejectionBudget` opens in a row bị **tripped**: không có opens mới cho cooldown window (`CopyDestinationTripped` alert fires), dừng rejection storm từ hammering (prop-firm) account. Vị trí hiện có vẫn được quản lý + đóng trong khi tripped; breaker auto-resets sau cooldown + successful copy clears counter.
- **Lot sanity ceiling** (C14) — absolute max copy size và/hoặc multiple-of-master cap. Tính toán copy vượt quá absolute cap, hoặc vượt quá `N×` master's own lot size, **hard-blocked** (surfaced as `lot_sanity` skip, counted on `cmind.copy.skipped`) không được đặt — defends chống lại catastrophic-oversize class (0.23-lot master turning into 3 lots trên mỗi receiver qua runaway multiplier hoặc rounding bug). Cả hai chiều mặc định `0` (off).

## Reliability & edge cases

Engine được xây dựng cho thực tế rằng bất cứ điều gì có thể thất bại bất kỳ lúc nào:

- **Slave-pending fill-correlation timeout** (C13) — mirrored slave pending mà master pending biến mất (không resting cũng không newly filled) cancelled sau correlation timeout, vì vậy slave copy không thể điền uncorrelated thành unmanaged position (`CopyPendingTimedOut`). Resync cũng làm sạch order-id-labelled filled-pending orphan.
- **Cross-broker pending-fill race** — một slave's own pending có thể fill (giá của nó hit) trong cửa sổ nhỏ trước khi master's fill/cancel event được xử lý. Điều đó để lại một slave position được gắn nhãn bằng source **order** id, mà các canonical close/SL-TP paths (keyed by source **position** id) sẽ miss. Trên master **fill** early slave fill được loại bỏ và thay thế bằng một market copy được gắn nhãn đúng cách — vì vậy đích đến kết thúc với chính xác **one** copy, không bao giờ doubled position; trên master **cancel** nó được đóng ngay lập tức (master không bao giờ lấy giao dịch). Cả hai hành động ngay lập tức, không chỉ trên resync tiếp theo. Một slave-side SL/TP hit mà đóng một bản sao mà master vẫn nắm giữ là source-driven và được mở lại trên reconcile tiếp theo (engine mirrors **master** events; nó không tiêu thụ destination-side executions).
- **Robust close/flatten** (M8) — closing orphan trên resync, hoặc flattening trên guard breach, tolerates position broker đã đóng (`POSITION_NOT_FOUND`): mỗi close chạy một cách độc lập, vì vậy một stale id không bao giờ abort resync hoặc để lại phần còn lại của account un-flattened.

- **Start với master đã có giao dịch** — khi bắt đầu host reconciles + mở bản sao cho vị trí hiện có của master.
- **Connection drops / desync** — trên reconnect host reconciles: mở bản sao bị mất, đóng orphans, re-labels pendings. Không có đơn hàng trùng lặp.
- **Order placement failure** — failure trên một đích đến logged, không bao giờ blocks đích đến khác.
- **Single valid token per cID** — cTrader invalidates cID's old access token moment new one issued. cMind swaps running host's token **in place** (re-auth trên live socket) vì vậy copying continues mà không dropping stream. Xem [token lifecycle](token-lifecycle.md).

## Auditability

Mỗi hành động phát ra sự kiện nhật ký có cấu trúc, được tạo từ nguồn (`LogMessages`) với profile id, destination cID, order/position ids, + values — order placed/skipped (với reason), partial close, protection applied, trailing applied, pending placed/amended/cancelled, expiry mirrored, market-range slippage mirrored, token swapped, resync summary. Đây là audit trail cho compliance + dispute resolution.

Bên cạnh logs, engine phát ra **OpenTelemetry metrics** trên `cMind.Copy` meter (registered trong shared OTel pipeline, exported qua OTLP / tới Azure Monitor giống như rest): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out tới tất cả destinations, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagged by destination), `cmind.copy.skipped` (tagged by reason), + `cmind.copy.failed`. Những điều này làm cho latency/slippage regression có thể đo được, không chỉ nhìn thấy trong dòng nhật ký — live suite asserts chúng chống lại budget.

## API

- `GET /api/copy/profiles` — list.
- `POST /api/copy/profiles` — create (với optional destination account ids).
- `GET /api/copy/profiles/{id}` — full detail incl. mỗi destination option.
- `POST /api/copy/profiles/{id}/destinations` — add a destination với đầy đủ option set.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remove.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes, decision filters, order-type filter, expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidation. Chạy chống lại `FakeTradingSession`, cTrader-faithful in-memory simulator.
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagation trên real Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip qua API + UI, full lifecycle.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) drive `CopyEngineHost` tới quiescence + assert convergence invariants. Xem [testing/stress-testing.md](../testing/stress-testing.md). Bộ suite này bề mặt + fixed real startup race: `OnReconnected` wired trước initial reference-load + resync, vì vậy socket flap trong startup có thể chạy second resync concurrently + corrupt host's non-concurrent state dictionaries — startup load + first resync hiện chạy dưới `_stateGate`.
- **Live** — real cTrader demo accounts; xem [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Xem [dev-credentials.md](../testing/dev-credentials.md) cho tệp credentials duy nhất live + E2E tiers đọc.
## Profile controls and destination management

Start/stop là icon buttons trên mỗi profile row (disabled khi hành động không áp dụng). Source and
destination accounts được hiển thị theo **account number**, không bao giờ one internal id. Clicking a profile
mở **dialog** để quản lý destination accounts của nó (add/remove với full per-destination settings).
