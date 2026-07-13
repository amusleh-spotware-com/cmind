---
description: "Phản chiếu tài khoản cTrader chính vào một hoặc nhiều tài khoản phụ — trên các nhà môi giới, trên các cID — với kiểm soát cho mỗi điểm đến + khối lượng tiền hóa giải."
---

# Sao chép giao dịch

Phản chiếu tài khoản cTrader **chính** vào một hoặc nhiều tài khoản **phụ** — trên các nhà môi giới, trên các cID — với kiểm soát cho mỗi điểm đến + khối lượng tiền hóa giải.

## Khái niệm

- **Sao chép hồ sơ** — một chính (`SourceAccountId`) + một hoặc nhiều **điểm đến**. Vòng đời: `Draft → Running → Paused → Stopped` (`Error` khi lỗi). Gốc tổng hợp: `CopyProfile` (sở hữu `CopyDestination`).
- **Điểm đến** — một tài khoản phụ + bộ quy tắc đầy đủ về cách sao chép chính vào nó. Tất cả cấu hình cho mỗi điểm đến, vì vậy một bản gốc nuôi dưỡng cả nô lệ bảo thủ + hung dữ cùng một lúc.
- **Lưu trữ công cụ sao chép** — công nhân chạy cho hồ sơ (`CopyEngineHost`). Đăng ký luồng thực thi chính, áp dụng từng sự kiện cho mỗi điểm đến.
- **Giám sát viên** — `CopyEngineSupervisor`, dịch vụ nền trên mỗi nút. Lưu trữ các hồ sơ được chỉ định, tự chữa lành trên cụm (xem [scaling](../deployment/scaling.md)).

## Những gì được phản chiếu

| Sự kiện chính | Hành động phụ |
|--------------|--------------|
| Mở vị trí thị trường / phạm vi thị trường | Mở một bản sao được định kích thước (được dán nhãn với id vị trí nguồn) |
| Lệnh đợi giới hạn / dừng / dừng-giới hạn | Đặt lệnh đợi phù hợp |
| Sửa đổi lệnh đợi | Sửa đổi lệnh đợi được phản chiếu tại chỗ |
| Lệnh đợi hủy / hết hạn | Hủy lệnh đợi được phản chiếu |
| Đóng một phần | Đóng cùng tỷ lệ của vị trí phụ |
| Tăng khối lượng (tăng khối lượng) | Mở khối lượng được thêm (chọn tham gia) |
| Thay đổi dừng-lỗ / dừng-theo dõi | Sửa đổi bảo vệ vị trí phụ |
| Đóng đầy đủ | Đóng bản sao phụ |

Mỗi bản sao **được dán nhãn với id vị trí/đơn hàng nguồn**. Sau khi kết nối lại, máy chủ lại xây dựng trạng thái từ hóa giải: mở các bản sao chính giữ nhưng phụ bị thiếu, đóng "mồ côi" phụ không còn giữ — **mà không lặp lại giao dịch**.

## Tạo một hồ sơ

Hộp thoại **Hồ sơ mới** trên trang Sao chép Giao dịch thu thập tất cả phía trước: tên hồ sơ, nguồn (chính) tài khoản, tài khoản điểm đến (phụ) (lựa chọn đa với nút **Chọn tất cả**; loại chính được chọn từ danh sách phụ), + bộ tùy chọn đầy đủ cho mỗi điểm đến bên dưới. Tất cả các đầu vào được **xác thực trước khi lưu** — tên/nguồn/điểm đến bị thiếu, tham số định kích thước không dương, ranh giới nhiều lô âm/không nhất quán, phần trăm rút vốn ngoài phạm vi, không bật loại lệnh, bộ lọc ký hiệu trống, hoặc các cặp bản đồ ký hiệu biến dạng xuất hiện dưới dạng danh sách lỗi + khóa lưu. Khi xác nhận, hồ sơ được tạo + mỗi phụ được chọn được thêm với cài đặt được chọn.

Các hành động hàng tôn trọng vòng đời: **Bắt đầu** được bật chỉ khi không chạy, **Dừng** + **Tạm dừng** chỉ khi chạy, **Xóa** bị vô hiệu hóa khi đang chạy + yêu cầu xác nhận trước khi xóa hồ sơ + điểm đến.

## Tùy chọn cho mỗi điểm đến

Đặt trong hộp thoại Hồ sơ mới, trên bảng cho mỗi điểm đến của trang Sao chép Giao dịch, hoặc thông qua `POST /api/copy/profiles/{id}/destinations`:

- **Định kích thước** (`MoneyManagementMode` + tham số): nhiều lô cố định, nhiều lô/bản ghi danh mục, cân bằng tỷ lệ/equity/lợi nhuận miễn phí, rủi ro cố định %, đòn bẩy cố định, tự động tỷ lệ, **rủi ro-%-từ-dừng** (M7). Cộng với ranh giới nhiều lô min/max + buộc-min-lot. **Risk-from-stop** quy mô điểm đến để nó rủi ro bằng phần trăm được cấu hình của *cân bằng riêng của nó*, bắt nguồn từ **khoảng cách dừng-lỗ của bản gốc** (`bản gốc rủi ro 2% → nô lệ tự động rủi ro 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Mở bản gốc **mà không** dừng-lỗi không có khoảng cách để định kích thước → sử dụng **lot dự phòng rủi ro tối đa** được cấu hình (M7) nếu được đặt, ngược lại bị bỏ qua (`no_stop_loss`) không đoán được. Kích thước tỷ lệ-**equity**/**lợi nhuận miễn phí** từ **equity** tài khoản thực (`balance + Σ floating P&L`, bắt nguồn theo cTrader Open API không cung cấp equity), không phải cân bằng thuần — vì vậy bản gốc ngồi trên lợi nhuận/lỗ mở định kích thước bản sao phải. Lợi nhuận được sử dụng không được hiển thị bởi API hóa giải, vì vậy lợi nhuận miễn phí được coi là equity (proxy quỹ sẵn có trung thực); các chế độ khác đọc cân bằng + bỏ qua vòng mua lại định giá thêm.
- **Bộ lọc hướng**: cả hai / chỉ dài / chỉ ngắn. **Đảo ngược**: lật bên (+ hoán đổi SL↔TP) cho bản sao tranh chấp.
- **Chỉ quản lý** (Bỏ qua-Giao dịch-Mới / Chỉ-Đóng): phản chiếu đóng, đóng một phần + các thay đổi bảo vệ trên các vị trí đã được sao chép, nhưng mở **không** vị trí/lệnh đợi mới (bỏ qua `manage_only`). Sử dụng để cuốn điểm đến xuống mà không cắt bản sao hiện có.
- **Đồng bộ-Mở-khi-bắt-đầu** / **Đồng bộ-Đóng-khi-bắt-đầu** (bật theo mặc định): khi **đầu tiên** của hồ sơ đồng bộ lại, có nên mở bản sao cho các vị trí tiền tồn tại của bản gốc hay không, + liệu có nên đóng bản sao mà bản gốc đóng khi hồ sơ dừng hay không. Cả hai chỉ áp dụng khi bắt đầu — kết nối lại giữa chạy luôn hóa giải đầy đủ vì vậy desync phục hồi bất kể.
- **Bản đồ ký hiệu** + **bộ lọc ký hiệu** (danh sách trắng / danh sách đen). Mỗi mục bản đồ ký hiệu mang **bộ nhân khối lượng mỗi ký hiệu** tùy chọn (ghi đè mỗi ký hiệu cMAM) định kích thước bản sao cho ký hiệu đó trên đỉnh của định kích thước điểm đến (1 = không thay đổi). Bản đồ toàn bộ nhập/xuất dưới dạng **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; các cột `Source,Destination,VolumeMultiplier`) — mỗi hàng được xác thực thông qua các đối tượng giá trị miền, vì vậy tệp biến dạng không thể tạo ra bản đồ không hợp lệ.
- **Cửa sổ giờ giao dịch** (C18) — cửa sổ UTC hằng ngày cho mỗi điểm đến (`start`/`end` phút trong ngày, kết thúc độc quyền; `start == end` = cả ngày). Các mở mới bên ngoài cửa sổ bị bỏ qua (`trading_hours`); cửa sổ có `start > end` quấn quanh nửa đêm (ví dụ 22:00–06:00). Các vị trí hiện có vẫn được quản lý.
- **Bộ lọc nhãn nguồn** (C18, tương đương cTrader của bộ lọc số kỳ diệu MT) — khi được đặt, chỉ sao chép các giao dịch chính có nhãn **chính xác** (ví dụ: giao dịch của một bot hoặc nhãn chỉ thủ công); ngược lại bị bỏ qua (`source_label`). Trống = sao chép tất cả. Được thực hiện trên `ExecutionEvent.SourceLabel` từ `TradeData.Label` của vị trí/đơn hàng chính, được tôn trọng trên resync quá.
- **Bảo vệ tài khoản** (ZuluGuard / Bảo vệ Tài khoản Toàn cầu) — xem **live equity** của điểm đến (`balance + Σ floating P&L`, được thăm dò mỗi `CopyDefaults.EquityGuardInterval`) so với sàn `StopEquity` và/hoặc trần `TakeEquity` tùy chọn. Khi vi phạm, áp dụng chế độ: **ChỉQuản lý** (dừng bản sao mới, giữ quản lý hiện tại), **Đông lạnh** (dừng mở), **Bán hết** (đóng **mỗi** bản sao trên điểm đến ngay lập tức). Sau khi kích hoạt, điểm đến bị khóa — không có mở mới cho đến khi máy chủ khởi động lại — + cảnh báo `CopyAccountProtectionTriggered` được nâng cao. `SellOut` yêu cầu `StopEquity`; `TakeEquity` phải ngồi trên `StopEquity`. **Không có cảnh báo bảo hành:** bán hết sử dụng thực thi thị trường — giống như tương đương của mọi đối thủ, không thể đảm bảo giá điền trong thị trường nhanh/gapped.
- **Nút hoảng loạn Làm phẳng-Tất cả** (C8) — `POST /api/copy/profiles/{id}/flatten` ngay lập tức đóng **mỗi** vị trí sao chép trên mỗi điểm đến + khóa lại các mở mới. Định tuyến trên quy trình: API đặt cờ, giám sát viên gửi đến máy chủ chạy (sử dụng lại kênh xoay token), điều này làm phẳng tại chỗ; cờ xóa nên kích hoạt chính xác một lần (cảnh báo `CopyFlattenAll`). Người dùng sau đó tạm dừng/dừng hồ sơ.
- **Vệ sĩ quy tắc công ty prop** (C7) — thực thi người dùng sao chép công ty prop yêu cầu. Mỗi điểm đến, **giới hạn mất hàng ngày** (mất từ equity mở ngày hôm đó) và/hoặc **giới hạn rút vốn theo dõi** (mất từ equity peak chạy), cả hai bằng tiền gửi. Khi vi phạm điểm đến **tự động làm phẳng** (mỗi bản sao đóng) + **bị khóa** phần còn lại của ngày UTC (bỏ qua mở `prop_lockout`); cảnh báo `CopyPropRuleBreached` kích hoạt. Khóa xóa khi ngày UTC cuộn qua (lấy đường cơ sở/peak mới). Chia sẻ cùng một bảo vệ-equity thăm dò như bảo vệ tài khoản.
- **Jitter thực thi** (C11, tắt theo mặc định) — độ trễ ngẫu nhiên `0..N` ms trước khi đặt mỗi bản sao, để khử tương quan dấu thời gian gần giống nhau trên các tài khoản **của chính bạn**. **Cảnh báo tuân thủ:** hỗ trợ các công ty prop cho phép sao chép — **không** công cụ để tránh công ty cấm nó; ở trong quy tắc của công ty bạn là trách nhiệm của bạn.
- **Khóa cấu hình** (C9) — đóng băng cài đặt điểm đến trong một khoảng thời gian (`POST …/destinations/{id}/lock` với phút). Khi bị khóa, điểm đến không thể bị xóa (tổng hợp từ chối với `CopyDestinationConfigLocked`) — lựa chọn cố ý chống lại những thay đổi vội vàng trong suốt rút vốn. Khóa hết hạn tự động tại dấu thời gian của nó.
- **Cảnh báo trước nhất quán** (C10) — cảnh báo (một lần mỗi ngày UTC) khi **lợi nhuận hằng ngày** của điểm đến đạt phần trăm được cấu hình của equity mở ngày hôm đó (`CopyConsistencyThresholdApproaching`), vì vậy quy tắc nhất quán công ty prop được tôn trọng *trước* khi nó bị bắn. Phía lợi nhuận, độc lập với khóa phía mất; chạy tắt cùng đường cơ sở ngày như vệ sĩ quy tắc prop.
- **Bộ lọc loại lệnh** — chọn chính xác loại lệnh chính nào để sao chép: thị trường, phạm vi thị trường, giới hạn, dừng, dừng-giới hạn (`CopyOrderTypes` cờ; mặc định tất cả). Tính linh hoạt kiểu cMAM.
- **Sao chép SL / Sao chép TP** — phản chiếu dừng-lỗ / lấy-lợi nhuận của bản gốc, hoặc quản lý bảo vệ độc lập.
- **Sao chép dừng-theo dõi**, **phản chiếu đóng một phần**, **phản chiếu tăng khối lượng** — mỗi cái có thể bật tắt độc lập.
- **Sao chép hết hạn đợi** (bật theo mặc định) — phản chiếu dấu thời gian hết hạn Good-Till-Date của lệnh đợi chính.
- **Sao chép trượt giá chính** (bật theo mặc định) — đối với các lệnh phạm vi thị trường + dừng-giới hạn, đặt lệnh phụ với trượt giá chính xác của bản gốc tính bằng điểm (giá cơ sở được lấy từ spot trực tiếp của phụ).
- **Lính canh**: max rút vốn %, cap mất hàng ngày, độ trễ sao chép tối đa, bộ lọc trượt giá (bỏ qua bản sao nếu giá phụ di chuyển vượt quá N pips từ mục nhập chính). **Độ trễ sao chép tối đa** được đo so với dấu thời gian máy chủ thực của sự kiện chính (`ExecutionEvent.ServerTimestamp`) thông qua `TimeProvider` được tiêm: tín hiệu cũ hơn độ trễ tối đa được cấu hình bị bỏ qua, vì vậy bản sao cũ không bao giờ được đặt muộn (trước đó độ trễ luôn bằng không + lính canh chết).
- **Chuẩn hóa độ chính xác SL/TP** (M6) — giá dừng-lỗ/lấy-lợi nhuận sao chép được làm tròn thành **độ chính xác chữ số của ký hiệu điểm đến** trước khi sửa đổi, vì vậy giá chính tại độ chính xác tốt hơn (hoặc sự không khớp chữ số trên nhà môi giới) không bao giờ kích hoạt `INVALID_STOPLOSS_TAKEPROFIT` của máy chủ.
- **Máy cắt mạch từ chối / Lính canh Follower** (G8) — điểm đến từ chối `CopyDefaults.RejectionBudget` mở liên tiếp bị **bắn**: không mở mới trong cửa sổ làm mát (`CopyDestinationTripped` cảnh báo kích hoạt), dừng bão từ chối không đập tài khoản (công ty prop). Các vị trí hiện có vẫn được quản lý + đóng khi bị bắn; máy cắt tự động đặt lại sau làm mát + bản sao thành công xóa bộ đếm.
- **Trần sáng tạo nhiều lô** (C14) — kích thước bản sao tối đa tuyệt đối và/hoặc bội số của chủ chính cap. Bản sao tính toán vượt quá giới hạn tuyệt đối, hoặc vượt quá `N×` kích thước lô của chính bản gốc, **hard-blocked** (xuất hiện dưới dạng bỏ qua `lot_sanity`, được tính trên `cmind.copy.skipped`) không được đặt — bảo vệ chống lại lớp quá kích thước thảm họa (0,23-lot chính biến thành 3 lô trên mỗi máy nhận qua bộ nhân chạy loạn hoặc lỗi làm tròn). Cả hai kích thước mặc định `0` (tắt).

## Độ tin cậy & trường hợp cạnh

Công cụ xây dựng cho sự thật rằng bất cứ điều gì có thể thất bại bất kỳ lúc nào:

- **Hết thời gian tương quan fill đợi phụ** (C13) — lệnh đợi phụ được phản chiếu có lệnh đợi chính biến mất (không nằm yên cũng không được điền mới) bị hủy sau hết thời gian tương quan, vì vậy bản sao phụ không thể điền không tương quan thành vị trí không được quản lý (`CopyPendingTimedOut`). Resync cũng làm sạch yêu tinh đợi điền được gắn nhãn bằng id đơn hàng.
- **Đóng/làm phẳng mạnh mẽ** (M8) — đóng yêu tinh trên resync, hoặc làm phẳng khi vi phạm canh gác, chịu đựng vị trí nhà môi giới đã đóng (`POSITION_NOT_FOUND`): mỗi lần đóng chạy độc lập, vì vậy một id cũ không bao giờ làm hỏng resync hoặc để lại phần còn lại của tài khoản chưa được làm phẳng.

- **Bắt đầu với bản gốc đã ở trong giao dịch** — khi bắt đầu máy chủ hóa giải + mở bản sao cho các vị trí hiện có của bản gốc.
- **Kết nối bị hỏng / desync** — khi kết nối lại máy chủ hóa giải: mở bản sao bị thiếu, đóng yêu tinh, ghi nhãn lại các phần đợi. Không có đơn đặt hàng trùng lặp.
- **Lỗi đặt lệnh** — lỗi trên một điểm đến đã được ghi, không bao giờ chặn các điểm đến khác.
- **Một mã thông báo hợp lệ duy nhất trên mỗi cID** — cTrader làm mất hiệu lực mã thông báo truy cập cũ của cID ngay lập tức khi cấp mã thông báo mới. cMind hoán đổi mã thông báo máy chủ chạy **tại chỗ** (xác thực lại trên ổ cắm trực tiếp) vì vậy sao chép tiếp tục mà không thả luồng. Xem [vòng đời mã thông báo](token-lifecycle.md).

## Khả năng kiểm toán

Mỗi hành động phát ra sự kiện nhật ký có cấu trúc, được tạo theo nguồn (`LogMessages`) với id hồ sơ, cID điểm đến, id đơn hàng/vị trí, + giá trị — đơn hàng được đặt/bỏ qua (với lý do), đóng một phần, bảo vệ áp dụng, theo dõi áp dụng, đợi đặt/sửa đổi/hủy, hết hạn được phản chiếu, trượt giá phạm vi thị trường được phản chiếu, mã thông báo hoán đổi, tóm tắt resync. Đây là dấu vết kiểm toán cho tuân thủ + giải quyết tranh chấp.

Bên cạnh nhật ký, công cụ phát ra **số liệu OpenTelemetry** trên đồng hồ `cMind.Copy` (được đăng ký trong đường dẫn OTel được chia sẻ, xuất qua OTLP / để theo dõi Azure như phần còn lại): `cmind.copy.latency` (sự kiện chính → phân phối, ms), `cmind.copy.dispatch.duration` (cổng quạt cho tất cả điểm đến, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (được gắn thẻ theo điểm đến), `cmind.copy.skipped` (được gắn thẻ theo lý do), + `cmind.copy.failed`. Chúng làm cho hồi quy độ trễ/trượt giá có thể đo được, không chỉ hiển thị trong dòng nhật ký — bộ kiểm tra trực tiếp khẳng định chúng so với ngân sách.

## API

- `GET /api/copy/profiles` — danh sách.
- `POST /api/copy/profiles` — tạo (với id tài khoản điểm đến tùy chọn).
- `GET /api/copy/profiles/{id}` — chi tiết đầy đủ bao gồm mọi tùy chọn điểm đến.
- `POST /api/copy/profiles/{id}/destinations` — thêm một điểm đến với bộ tùy chọn đầy đủ.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — xóa.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — vòng đời.

## Các bài kiểm tra

- **Đơn vị** (`tests/UnitTests/CopyTrading`) — chế độ định kích thước, bộ lọc quyết định, bộ lọc loại lệnh, sao chép hết hạn, trượt giá phạm vi thị trường/dừng-giới hạn, bật tắt SL/TP, đóng một phần, sửa đổi/hủy đợi, bắt đầu-với-mở, ngắt kết nối→desync→resync, hoán đổi mã thông báo tại chỗ, làm mất hiệu lực trên cID. Chạy so với `FakeTradingSession`, mô phỏng trong bộ nhớ trung thực cTrader.
- **Tích hợp** (`tests/IntegrationTests/CopyLive`) — yêu cầu nút-親和/cho thuê, lan truyền phiên bản mã thông báo trên Postgres thực.
- **E2E** (`tests/E2ETests`) — điểm đến-tùy chọn vòng tròn qua API + UI, vòng đời đầy đủ.
- **Căng thẳng / DST** (`tests/StressTests`) — kiểm tra mô phỏng xác định: khối lượng công việc ngẫu nhiên được ghi hạt + tiêm lỗi (flap ổ cắm, từ chối đơn hàng, từ chối phạm vi thị trường, xoay mã thông báo, cái chết nút) lái `CopyEngineHost` đến tĩnh lặng + khẳng định bất biến hội tụ. Xem [testing/stress-testing.md](../testing/stress-testing.md). Bộ kiểm tra này bề mặt + sửa chữa cuộc đua khởi động thực: `OnReconnected` kết nối trước khi tải tham chiếu ban đầu + resync, vì vậy ổ cắm flap trong suốt khởi động có thể chạy resync thứ hai đồng thời + tham nhũng từ điển trạng thái không đồng thời của máy chủ — tải khởi động + resync đầu tiên hiện chạy dưới `_stateGate`.
- **Trực tiếp** — tài khoản demo cTrader thực; xem [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Xem [dev-credentials.md](../testing/dev-credentials.md) đối với tệp thông tin xác thực duy nhất tầng live + E2E đọc.
