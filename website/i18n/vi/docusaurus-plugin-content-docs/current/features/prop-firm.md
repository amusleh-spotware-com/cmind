---
description: "Công ty prop bán lẻ (kiểu FTMO) bán tài khoản đánh giá: nhà giao dịch phải đạt mục tiêu lợi nhuận trong khi ở bên trong giới hạn rủi ro (mất hàng ngày tối đa, tối đa..."
---

# Mô phỏng thách thức Prop-firm

Công ty prop bán lẻ (kiểu FTMO) bán **tài khoản đánh giá**: nhà giao dịch phải đạt mục tiêu lợi nhuận trong khi ở bên trong giới hạn rủi ro (mất hàng ngày tối đa, rút vốn tối đa/theo dõi, nhất quán, giới hạn thời gian) trước khi được tài trợ. cMind cho phép người dùng tạo **thách thức tùy chỉnh của bất kỳ hình dạng ngành nào**, ràng buộc với `TradingAccount`, **chạy như hoạt động sao chép giao dịch** — bắt đầu/dừng, được lưu trữ trên nút, theo dõi **trực tiếp qua cTrader Open API**. Tổng hợp đánh giá mọi quy tắc một cách xác định; khi vượt qua hoặc vi phạm, kết thúc thách thức, đánh dấu nó, cảnh báo người dùng.

## Miền (Bối cảnh giới hạn: PropFirm)

`PropFirmChallenge` = gốc tổng hợp (mô-đun `Core.PropFirm`), tham chiếu `TradingAccount` của nó chỉ bằng id mạnh (không cross-aggregate FK). Sở hữu đánh giá quy tắc, máy trạng thái pha/trạng thái, cho thuê nút.

### Các đối tượng giá trị & bộ quy tắc

- **`Money`** (không âm), **`MoneyAmount`** (được ký), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — đọc được cung cấp cho tổng hợp.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — sự kiện không phải equity.
- **`DailyLossLimit`** `(percent, basis)` — cơ sở `Equity` (intraday, bao gồm floating P&L) hoặc `Balance` (chỉ nhận ra).
- **`DrawdownLimit`** — `Static` (từ cân bằng bắt đầu), `TrailingPercent` (từ equity peak), hoặc `TrailingThresholdDollar` (theo dõi equity peak theo số tiền cố định đô la, sau đó **khóa tại cân bằng bắt đầu** khi equity đạt ngưỡng — kiểu tương lai).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — chặn pass trong khi một ngày chiếm ưu thế lợi nhuận tổng cộng.
- **`ChallengeRules`** mang trên cùng cộng với `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`, `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Toán học quy tắc sống trên VOs (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); tổng hợp điều phối.

### Loại thách thức & mẫu

`ChallengeTemplates.For(kind)` xây dựng phần cơ sở hợp lệ cho `OnePhase`, `TwoPhase`, `ThreePhase`, `InstantFunding`, hoặc `Custom` (kiểm soát toàn bộ). Giao diện người dùng điền trước mẫu; người dùng có thể điều chỉnh bất kỳ trường nào.

### Pha & trạng thái

- **Pha:** `Evaluation → Verification → Funded` (bước đơn bỏ qua Xác minh).
- **Trạng thái:** `Active`, `Passed`, `Failed`, cộng với vòng đời `Stopped` (theo dõi bị tạm dừng) — `Create` bắt đầu thách thức `Active`; `Stop()`/`Resume()` bật/tắt `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`, `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Đánh giá quy tắc

- **`RecordEquity(EquitySnapshot, now)`** — cuộn ngày giao dịch tại ranh giới ngày (nắm bắt lợi nhuận của ngày trước cho quy tắc nhất quán), cập nhật peak/daily peaks, sau đó **thất bại khi vi phạm đầu tiên** (mất hàng ngày → rút vốn → giới hạn thời gian → không hoạt động, theo thứ tự) hoặc tự tiến pha khi mục tiêu lợi nhuận, ngày giao dịch tối thiểu, yêu cầu nhất quán tất cả được đáp ứng. Các bản chụp không theo thứ tự và ghi lại trên thách thức đầu cuối ném `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — đánh giá quy tắc hành vi (vị trí mở tối đa, giữ cuối tuần, tin tức giao dịch), dấu hoạt động cho quy tắc không hoạt động.
- Mềm **`PropFirmDrawdownWarning`** kích hoạt một lần khi sử dụng equity vượt qua ngưỡng có thể cấu hình.

Sự kiện miền: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`, `PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Theo dõi trực tiếp (Thực thi) — lưu trữ nút, tự chữa lành

Theo dõi phản chiếu ngăn xếp lưu trữ giao dịch sao chép chính xác; prop tracker = **chỉ đọc** tương tự của công cụ sao chép.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` trên mỗi nút, được gating trên `App:PropFirm:Enabled`. Mỗi chu kỳ **yêu sách** những thách thức hoạt động trên cho thuê tự chữa lành (`AssignedNode` + `LeaseExpiresAt`; những thách thức của nút chết được yêu sách một khi cho thuê hết hạn — cùng một yêu cầu `ExecuteUpdate` nguyên tử như giao dịch sao chép, vì vậy hai nút không bao giờ theo dõi kép), gia hạn cho thuê, đẩy mã thông báo được xoay tại chỗ, dừng máy chủ có thách thức trái `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — một mỗi thách thức. Mở `IOpenApiTradingSession` cho tài khoản và, trên `App:PropFirm:EquityPollInterval`, tính toán lại equity trực tiếp, cung cấp cho tổng hợp. Hoán đổi mã thông báo truy cập tại chỗ khi xoay (không thả phiên). Thoát khi thách thức không còn `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — toán học equity trung thành cTrader. Equity **không** được cung cấp bởi Open API, vì vậy bắt nguồn: `equity = balance + Σ(unrealized P&L)`, trong đó P&L của mỗi vị trí là `priceDifference × units × quote→deposit rate + swap + commission` (`units = wire volume / 100`; long revalues at bid, short at ask). Cân bằng từ `ProtoOATrader`; vị trí (giá nhập, swap, commission) từ hòa giải; bid/ask trực tiếp từ đăng ký spot. Thuần túy và bị cô lập — currency-conversion hot spot được kiểm tra đơn vị trên chính nó.

## Cảnh báo

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) đăng ký để vượt qua/vi phạm/cảnh báo sự kiện miền (được đăng ký là `IDomainEventHandler<>`, được phân phối sau khi thành công `SaveChanges`), thông báo cho người dùng thông qua cảnh báo/dấu vết kiểm toán có cấu trúc (`LogMessages`). Giao diện người dùng trực tiếp phản ánh cùng một thay đổi trạng thái. Điều này = phản ứng cross-context — không bao giờ biến tổng hợp thách thức.

## API (`/api/prop-firm`, tính năng `PropFirm`, vai trò User+)

| Phương thức | Tuyến | Mục đích |
|--------|-------|---------|
| GET | `/challenges` | liệt kê thách thức của người dùng (loại, pha, trạng thái, live equity, cho thuê) |
| GET | `/challenges/{id}` | một thách thức |
| GET | `/templates` | phần cơ sở ngành cho hộp thoại tạo |
| POST | `/challenges` | tạo từ mẫu **hoặc** bộ quy tắc hoàn toàn tùy chỉnh |
| POST | `/challenges/{id}/start` | theo dõi lại (Stopped → Active) |
| POST | `/challenges/{id}/stop` | dừng theo dõi (Active → Stopped, phát hành cho thuê) |
| POST | `/challenges/{id}/equity` | ghi lại bản chụp equity → re-evaluate (đường dẫn thủ công/không có live-feed) |
| DELETE | `/challenges/{id}` | soft-delete (bị chặn khi Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` hiển thị danh sách/tạo(from template)/record-equity/start/stop, được gating trên tính năng `PropFirm`.

UI: `/prop-firm` (nav *Prop Firm*, được gating bởi cờ `PropFirm`) liệt kê những thách thức với hành động hàng **Start/Stop/Delete** (Start khi Stopped, Stop khi Active, Delete bị vô hiệu hóa khi Active), tạo chúng thông qua `NewPropFirmChallengeDialog` (bộ chọn mẫu + trình chỉnh sửa quy tắc đầy đủ). Tất cả tạo/chỉnh sửa thông qua hộp thoại MudBlazor.

## Nguồn cấp dữ liệu equity trực tiếp — đã phân giải

Khoảng cách "không có live account P&L feed" trước đó đã đóng: khi `App:PropFirm:Enabled` được đặt, nút theo dõi tài khoản trực tiếp qua Open API, cung cấp equity tự động. Mà không có nó (mặc định), miền và đường dẫn **manual-equity** (`POST …/equity`) chạy không thay đổi — không cần thông tin xác thực cTrader cho build/test/E2E.

## Các bài kiểm tra

- **Đơn vị** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (tự tiến pha, ngày tối thiểu, rút vốn tĩnh/theo dõi, mất hàng ngày, lính canh đầu cuối/không theo thứ tự); `PropFirmChallengeRulesTests` (cân bằng vs cơ sở mất hàng ngày equity, trailing-threshold-dollar trail+lock, consistency block/allow, time-limit, inactivity, max-exposure, weekend, news, stop/resume, lease boundary, pass releases lease, drawdown warning); `PropFirmValueObjectTests` (VO ranges + rule-VO maths); `PropFirmEquityCalculatorTests` (long/short P&L, swap/commission, quote→deposit conversion, missing pricing); `PropFirmTrackingHostTests` (live equity lái pass/fail chống extended fake session); `PropFirmAlertNotifierTests`. Thời gian rõ ràng / `FakeTimeProvider` — không đọc wall-clock.
- **Tích hợp** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (vòng tròn + record-equity + soft-delete, enriched-rules + lease round-trip) và `PropFirmTrackingLeaseTests` (yêu sách, tranh chấp cho thuê, yêu sách lại sau khi hết hạn trên hai nhận dạng nút) trên Postgres thực.
- **E2E** — `E2ETests/PropFirmTests.cs`: tạo + record-equity để `Passed`; stop→start→breach flow; điểm cuối mẫu.
- **Căng thẳng / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: các luồng equity/activity ngẫu nhiên được ghi hạt (ngày cuộn, spikes, crashes, duplicate + out-of-order snapshots, exposure/weekend/news) trên nhiều thách thức quy tắc hỗn hợp, khẳng định các trạng thái đầu cuối exact-once dính, bất biến peak-bounds-current, những thất bại hợp lý.

## Cấu hình (`App:PropFirm`)

`Enabled` (tắt theo mặc định), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`, `DrawdownWarnThresholdPercent`, `NodeName`.
