---
description: "Xây dựng, chạy, backtest cBots cTrader (C# và Python, cả .NET) từ trình soạn thảo Monaco trong trình duyệt, chạy trên hình ảnh chính thức ghcr.io/spotware/ctrader-console."
---

# Xây dựng & backtest cBots

Xây dựng, chạy, backtest cBots cTrader (C# **và** Python, cả .NET) từ trình soạn thảo Monaco trong trình duyệt, chạy trên hình ảnh chính thức `ghcr.io/spotware/ctrader-console`.

## Xây dựng

- Trang **Builder** lưu trữ trình soạn thảo Monaco; `CBotBuilder` biên dịch dự án với `dotnet build` **trong container tạm thời** (`AppOptions.BuildImage`, thư mục làm việc được gắn tại `/work`), do đó các mục tiêu MSBuild của người dùng không đáng tin cậy không thể truy cập máy chủ. Khôi phục NuGet được lưu trong bộ nhớ cache trên các lần dựng thông qua tập hợp được chia sẻ. Máy chủ Web cần quyền truy cập vào ổ cắm Docker.
- Các mẫu bắt đầu C# + Python nằm trong `src/Nodes/Builder/Templates/`.

## Chạy & backtest

- **Instances** = phân cấp trạng thái TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Quá trình chuyển đổi thay thế thực thể (thay đổi id), id container được mang theo.
- `NodeScheduler` chọn nút đủ điều kiện ít tải nhất; `ContainerDispatcherFactory` định tuyến đến agent HTTP nút từ xa hoặc bộ điều phối Docker cục bộ.
- Các bộ khảo sát hoàn thành điều hòa các container đã thoát (các container backtest tự thoát qua `--exit-on-stop`); báo cáo hiện diện → hoàn thành (lưu `ReportJson`), mất → thất bại.
- Nhật ký container trực tiếp truyền phát đến trình duyệt qua SignalR; đường cong vốn backtest được phân tích từ báo cáo + được vẽ biểu đồ.

## Dữ liệu thị trường backtest được lưu trong bộ nhớ cache trên mỗi tài khoản

cTrader Console tải xuống dữ liệu tick/bar lịch sử vào `--data-dir` của nó. Thư mục đó là **bộ nhớ cache ổn định, liên tục được khóa trên tài khoản giao dịch** (số tài khoản của nó) — được gắn từ đĩa của nút tại đường dẫn container của nó (`/mnt/data`), một **gắn riêng, không lồng nhau** từ thư mục làm việc trên mỗi instance. Vì vậy, mỗi backtest trên cùng tài khoản **tái sử dụng** dữ liệu đã tải xuống thay vì tải xuống lại ở mỗi lần chạy. (Trước đó thư mục dữ liệu nằm dưới thư mục làm việc trên mỗi instance, có id thay đổi ở mỗi lần chạy, buộc phải tải xuống mới mỗi lần backtest.) Thư mục làm việc dung tích của mỗi instance vẫn chứa algo, params, mật khẩu và báo cáo; bộ nhớ cache dữ liệu được chia sẻ được tính trong mức sử dụng dữ liệu backtest của nút và được xóa bởi hành động làm sạch nút.

## Cài đặt backtest

Hộp thoại **Backtest** hiển thị cài đặt backtest cTrader Console có thể điều chỉnh bởi người dùng, do đó bạn không bao giờ phải chạm vào dòng lệnh:

- **Symbol / Timeframe** — timeframe là **danh sách thả xuống của mỗi khoảng thời gian cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` và các khoảng thời gian Renko/Range/Heikin), trong cách viết chữ hoa chính tắc của bảng điều khiển, do đó bạn luôn chọn một `--period` hợp lệ.
- **From / To** — cửa sổ backtest (`--start` / `--end`).
- **Data mode** — một trong ba chế độ cTrader (`--data-mode`): **Tick data** (`tick`, chính xác), **m1 bars** (`m1`, nhanh), hoặc **Open prices only** (`open`, nhanh nhất).
- **Starting balance** — mặc định là `10000` (`--balance`). Một **số dư 0 không thực hiện giao dịch và khiến cTrader phát ra báo cáo trống, sau đó bị sập** ("Message expected"), do đó luôn gửi số dư khác 0.
- **Commission** — `--commission`.
- **Spread** — `--spread`, một **trường số trong pips không thể dưới 0**. Nó **ẩn trong chế độ Tick data**, nơi cTrader lấy spread từ chính dữ liệu tick (không gửi `--spread`).

Thư mục dữ liệu (`--data-file` / `--data-dir`) được quản lý bởi chính ứng dụng (bộ nhớ cache mỗi tài khoản, xem trên), không được hiển thị trong hộp thoại.

:::note cTrader sập trên một backtest trống
Nếu một backtest tạo ra **không kết quả** — không có giao dịch, hoặc không có dữ liệu thị trường cho ngày/ký hiệu đã chọn — trình ghi báo cáo riêng của cTrader Console ném `Message expected` và thoát mà không có báo cáo. Ứng dụng không thể sửa lỗi thượng nguồn đó, nhưng nó phát hiện và đánh dấu instance **Failed** với lý do hành động ("không có kết quả backtest cho phạm vi đã chọn…") thay vì dấu vết ngăn xếp thô. Chọn phạm vi ngày rộng hơn có dữ liệu thị trường có sẵn và thử lại.
:::

## Trang chi tiết instance

Mở một instance (`/instance/{id}`) hiển thị trạng thái trực tiếp, nhật ký của nó và — đối với backtest — đường cong vốn. **Tiêu đề thẻ trình duyệt** phản ánh instance cụ thể (**tên cBot · kind · symbol**, ví dụ: `TrendBot · Backtest · EURUSD`) vì vậy tab chạy trực tiếp và tab backtest có thể phân biệt ngay được. Một chạy và một backtest của cùng cBot được theo dõi là **lineages** riêng biệt (một id lineage ổn định được mang qua các chuyển đổi trạng thái), do đó trang theo dõi chính xác một instance và không bao giờ trộn dữ liệu chạy với dữ liệu backtest.

## Điều khiển vòng đời instance

Mỗi hàng instance (và trang chi tiết của nó) có các điều khiển chính xác trạng thái. Một instance **hoạt động** hiển thị **Stop**; một instance **terminal** (Stopped / Completed / Failed) hiển thị **Start (▶)** để khởi chạy lại nó với cùng cBot, tài khoản, symbol, timeframe, ParamSet và image (một lần chạy khởi động lại dưới dạng chạy, một backtest dưới dạng backtest). Nhấp vào Stop hiển thị thông báo "Stopping…" và vô hiệu hóa biểu tượng cho đến khi giải quyết, và một lần chạy mới tạo xuất hiện trong danh sách ngay lập tức — không cần tải lại trang.

Nhật ký bảng điều khiển được **lưu khi một instance kết thúc** — cho một lần chạy (trên Stop) và cho **backtest** (khi hoàn thành) — do đó nhật ký của lần chạy cuối cùng vẫn có thể xem trên trang chi tiết và, thông qua thanh công cụ nhật ký, **sao chép vào bộ nhớ tạm** (biểu tượng Sao chép nhật ký) hoặc **tải xuống** (biểu tượng Tải xuống nhật ký) ngay cả sau khi container biến mất. Cả hai hoạt động trên toàn bộ nhật ký bảng điều khiển của instance, không chỉ phần đuôi trên màn hình.

Một `.algo` được **tải lên** chưa bao giờ được xây dựng ở đây, do đó cột **Last Build** của nó trên trang cBots được để trống (nó chỉ hiển thị thời gian dựng cho các cBot bạn dựng trong trình duyệt).

## Chỉnh sửa & chạy lại một instance đã dừng

Một instance **đã dừng** (chạy hoặc backtest) có điều khiển **Edit** — một biểu tượng trên hàng của nó trong danh sách **và** bên cạnh Start/Stop trên trang chi tiết của nó — mở hộp thoại **được điền sẵn** với cấu hình hiện tại của nó. Bạn có thể thay đổi **tài khoản giao dịch, symbol, timeframe, ParamSet và image tag** (và, đối với backtest, **cửa sổ và tất cả cài đặt backtest** trên), sau đó **Save & start** khởi chạy lại nó với cài đặt mới (thay thế instance đã dừng). Điều khiển này **bị vô hiệu hóa khi instance hoạt động** — chỉ instance đã dừng mới có thể được chỉnh sửa.

## Chạy từ trình soạn thảo mã

Nhấp **Run** trong trình soạn thảo mã mở hộp thoại thay vì kích hoạt lần chạy mù, được mã hóa cứng:

- **Trading account** (bắt buộc) — tài khoản cTrader mà cBot kết nối.
- **Parameter set** (tùy chọn) — chọn tập hợp hiện có, hoặc để trống để chạy với **giá trị tham số mặc định của cBot**. Nút **+** bên cạnh bộ chọn tạo ParamSet mới nội tuyến (xem bên dưới) và chọn nó.
- **Symbol / Timeframe** mặc định là `EURUSD` / `h1` và có thể thay đổi; **Cancel** hoặc **Run**.

Khi **Run**, trình soạn thảo lưu + dựng nguồn hiện tại, bắt đầu instance trên tài khoản đã chọn với các tham số đã chọn, sau đó xem nhật ký container trực tiếp. (Luồng nhật ký chuyển tiếp cookie xác thực của người dùng đã đăng nhập đến hub SignalR `/hubs/logs`, do đó nó kết nối thay vì thất bại với `Invalid negotiation response received`.)

## Bộ tham số

Một **parameter set** là tập hợp ghi đè tham số cBot có tên, tái sử dụng được lưu dưới dạng đối tượng JSON phẳng ánh xạ mỗi tên tham số đến giá trị vô hướng, ví dụ: `{"Period": 14, "Label": "trend"}`. Tại thời điểm chạy/backtest nó trở thành tệp cTrader `params.cbotset` (`{ "Parameters": { … } }`). Bạn có thể tạo/chỉnh sửa tập hợp dưới dạng JSON thô từ hộp thoại **Parameter sets** của cBot hoặc nội tuyến từ hộp thoại Run.

Mỗi ParamSet **thuộc về một cBot**: hộp thoại New Parameter Set liệt kê tất cả cBots của bạn và bạn **phải chọn một** — tạo bị chặn cho đến khi chọn cBot. **Tên của tập hợp là duy nhất mỗi cBot**: tạo hoặc đổi tên tập hợp thành tên mà tập hợp khác của cùng cBot đã sử dụng bị từ chối (lỗi rõ ràng trong hộp thoại, `409 Conflict` tại API). Tên tương tự có thể được tái sử dụng trên **cBot khác**.

JSON được **xác thực** khi lưu: nó phải là đối tượng phẳng duy nhất có các giá trị là tất cả vô hướng (string / number / bool). Gốc không phải đối tượng, mảng, đối tượng lồng nhau, giá trị `null`, hoặc JSON không được định dạng sẽ bị từ chối (lỗi rõ ràng trong hộp thoại, `400 Bad Request` tại API). Đối tượng trống `{}` được cho phép và có nghĩa là "không có ghi đè".

## Ghi chú CLI Bảng điều khiển cTrader

Backtests cần `--data-mode` (mặc định `m1`), ngày là `dd/MM/yyyy HH:mm`, và `params.cbotset` JSON đối số vị trí; `run` từ chối `--data-dir` (chỉ backtest). Xem `ContainerCommandHelpers`.

## Nodes & scale

Dung lượng thực thi mở rộng bằng cách thêm agent nodes (tự đăng ký + heartbeat). Xem [node discovery](../operations/node-discovery.md) và [scaling](../deployment/scaling.md).

## Yêu cầu tài khoản giao dịch

Chạy hoặc backtest cBot cần tài khoản giao dịch cTrader để kết nối. Cho đến khi bạn thêm một tài khoản trong **Trading accounts**, các nút **Run New cBot** / **Backtest New cBot** bị vô hiệu hóa (có tooltip) và trang hiển thị lời nhắc liên kết đến thiết lập tài khoản — bạn không còn gặp lỗi `stream connect failed` thô từ bot không có tài khoản.
